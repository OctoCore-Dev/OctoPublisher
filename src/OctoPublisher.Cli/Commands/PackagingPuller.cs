namespace OctoPublisher.Cli.Commands;

public static class PackagingPuller
{
    public static async Task<int> ExecuteAsync(
        string platform,
        string targetDir,
        string? targetId,
        string? appId,
        string? packageName,
        string? serviceAccount,
        string? lang,
        string? market,
        MStoreCredentials? mstoreCreds,
        CancellationToken ct = default)
    {
        AnsiTerminal.Header($"OctoPublisher: Импорт ассетов магазина [{platform}]");

        var effectivePlatform = platform.ToLowerInvariant();
        var successCount = 0;
        var failCount = 0;

        if (effectivePlatform is "all" or "windows")
        {
            var winResult = await PullWindowsAsync(targetDir, targetId, appId, lang, market, mstoreCreds, ct);
            if (winResult == 0)
            {
                successCount++;
            }
            else
            {
                failCount++;
            }
        }

        if (effectivePlatform is "all" or "android")
        {
            var androidResult = await PullAndroidAsync(targetDir, targetId, packageName, serviceAccount, lang, ct);
            if (androidResult == 0)
            {
                successCount++;
            }
            else
            {
                failCount++;
            }
        }

        if (failCount > 0 && successCount == 0)
        {
            return 1;
        }

        AnsiTerminal.Success($"Импорт Store as Code завершён в каталоге '{targetDir}'.");
        AnsiTerminal.Info("Запустите 'octopublish test' для проверки соответствия импортированных ассетов правилам сторов.");
        return 0;
    }

    private static async Task<int> PullWindowsAsync(
        string targetDir,
        string? targetId,
        string? configuredAppId,
        string? requestedLang,
        string? requestedMarket,
        MStoreCredentials? mstoreCreds,
        CancellationToken ct)
    {
        var resolvedId = !string.IsNullOrWhiteSpace(targetId) && !targetId.Contains('.')
            ? targetId
            : (configuredAppId ?? mstoreCreds?.AppId);

        if (string.IsNullOrWhiteSpace(resolvedId))
        {
            AnsiTerminal.Error("[Windows] Не указан Product ID Microsoft Store. Передайте ID позиционным аргументом или через флаг --app-id.");
            return 1;
        }

        AnsiTerminal.Step("Windows", $"Запрос метаданных Microsoft Store для Product ID '{resolvedId}'...");

        var catalogClient = new MStoreCatalogClient();
        var pulledLocales = 0;

        var marketsToQuery = new List<(string Market, string Lang)>();
        if (!string.IsNullOrWhiteSpace(requestedLang))
        {
            var norm = LocaleRegistry.NormalizeLocale(requestedLang);
            var mkt = !string.IsNullOrWhiteSpace(requestedMarket) ? requestedMarket.ToUpperInvariant() : (norm.StartsWith("ru", StringComparison.OrdinalIgnoreCase) ? "RU" : "US");
            marketsToQuery.Add((mkt, norm));
        }
        else
        {
            marketsToQuery.Add(("RU", "ru-RU"));
            marketsToQuery.Add(("US", "en-US"));
        }

        foreach (var (mkt, lng) in marketsToQuery)
        {
            try
            {
                var product = await catalogClient.GetProductAsync(resolvedId, mkt, lng, ct);
                if (product?.LocalizedProperties == null || product.LocalizedProperties.Count == 0)
                {
                    continue;
                }

                foreach (var loc in product.LocalizedProperties)
                {
                    var rawLang = loc.Language?.Trim() ?? string.Empty;
                    if (string.Equals(rawLang, "neutral", StringComparison.OrdinalIgnoreCase))
                    {
                        // В каталоге Microsoft Store "neutral" - внутренний fallback.
                        // Если в ответе есть нормальные языки кроме neutral, пропускаем нейтральный дубль
                        if (product.LocalizedProperties.Any(p => !string.IsNullOrWhiteSpace(p.Language) && !string.Equals(p.Language, "neutral", StringComparison.OrdinalIgnoreCase)))
                        {
                            continue;
                        }

                        // Если опрашивался не русский рынок (например en-US), но вернулся русский neutral-fallback, не затираем
                        if (!lng.StartsWith("ru", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        rawLang = lng;
                    }

                    var localeCode = !string.IsNullOrWhiteSpace(rawLang)
                        ? LocaleRegistry.NormalizeLocale(rawLang)
                        : lng;

                    if (string.Equals(localeCode, "neutral", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var storeDir = Path.Combine(targetDir, "store", "windows", localeCode);
                    _ = Directory.CreateDirectory(storeDir);

                    if (!string.IsNullOrWhiteSpace(loc.ProductTitle))
                    {
                        await File.WriteAllTextAsync(Path.Combine(storeDir, "title.txt"), loc.ProductTitle.Trim(), Encoding.UTF8, ct);
                    }

                    if (!string.IsNullOrWhiteSpace(loc.ProductDescription))
                    {
                        await File.WriteAllTextAsync(Path.Combine(storeDir, "full_description.txt"), loc.ProductDescription.Trim(), Encoding.UTF8, ct);
                    }

                    if (!string.IsNullOrWhiteSpace(loc.ShortDescription))
                    {
                        await File.WriteAllTextAsync(Path.Combine(storeDir, "short_description.txt"), loc.ShortDescription.Trim(), Encoding.UTF8, ct);
                    }

                    if (!string.IsNullOrWhiteSpace(loc.ReleaseNotes))
                    {
                        await File.WriteAllTextAsync(Path.Combine(storeDir, "release_notes.txt"), loc.ReleaseNotes.Trim(), Encoding.UTF8, ct);

                        var whatsNewDir = Path.Combine(targetDir, "whatsnew");
                        _ = Directory.CreateDirectory(whatsNewDir);
                        await File.WriteAllTextAsync(Path.Combine(whatsNewDir, $"whatsnew-{localeCode}.txt"), loc.ReleaseNotes.Trim(), Encoding.UTF8, ct);
                    }

                    if (loc.Features != null && loc.Features.Count > 0)
                    {
                        var featuresContent = string.Join(Environment.NewLine, loc.Features.Where(f => !string.IsNullOrWhiteSpace(f)).Select(f => f.Trim()));
                        if (!string.IsNullOrWhiteSpace(featuresContent))
                        {
                            await File.WriteAllTextAsync(Path.Combine(storeDir, "features.txt"), featuresContent, Encoding.UTF8, ct);
                        }
                    }

                    // Политика конфиденциальности: сохраняем только валидный веб-URL (http/https)
                    if (!string.IsNullOrWhiteSpace(loc.PrivacyUrl) &&
                        Uri.TryCreate(loc.PrivacyUrl, UriKind.Absolute, out var pUri) &&
                        (pUri.Scheme == Uri.UriSchemeHttp || pUri.Scheme == Uri.UriSchemeHttps))
                    {
                        await File.WriteAllTextAsync(Path.Combine(storeDir, "privacy.txt"), loc.PrivacyUrl.Trim(), Encoding.UTF8, ct);
                    }

                    // Контакт техподдержки: email, mailto или веб-форма
                    if (!string.IsNullOrWhiteSpace(loc.SupportUri))
                    {
                        await File.WriteAllTextAsync(Path.Combine(storeDir, "support_contact.txt"), loc.SupportUri.Trim(), Encoding.UTF8, ct);
                    }

                    // Официальный веб-сайт разработчика
                    if (!string.IsNullOrWhiteSpace(loc.PublisherWebsiteUri) &&
                        Uri.TryCreate(loc.PublisherWebsiteUri, UriKind.Absolute, out var wUri) &&
                        (wUri.Scheme == Uri.UriSchemeHttp || wUri.Scheme == Uri.UriSchemeHttps))
                    {
                        await File.WriteAllTextAsync(Path.Combine(storeDir, "website.txt"), loc.PublisherWebsiteUri.Trim(), Encoding.UTF8, ct);
                    }

                    if (loc.Images != null && loc.Images.Count > 0)
                    {
                        var logoImg = loc.Images
                            .Where(i => i.ImagePurpose is "Logo" or "Tile" or "AppIconLarge" && !string.IsNullOrWhiteSpace(i.Uri))
                            .OrderBy(i => Math.Abs(i.Width - 300) + Math.Abs(i.Height - 300))
                            .FirstOrDefault();

                        if (logoImg != null && !string.IsNullOrWhiteSpace(logoImg.Uri))
                        {
                            var bytes = await catalogClient.DownloadImageBytesAsync(logoImg.Uri, ct);
                            if (bytes != null && bytes.Length > 0)
                            {
                                await File.WriteAllBytesAsync(Path.Combine(storeDir, "icon.png"), bytes, ct);
                            }
                        }

                        var bannerImg = loc.Images
                            .FirstOrDefault(i => (i.ImagePurpose is "SuperHeroArt" || (i.Width == 1920 && i.Height == 1080)) && !string.IsNullOrWhiteSpace(i.Uri));

                        if (bannerImg != null && !string.IsNullOrWhiteSpace(bannerImg.Uri))
                        {
                            var bytes = await catalogClient.DownloadImageBytesAsync(bannerImg.Uri, ct);
                            if (bytes != null && bytes.Length > 0)
                            {
                                await File.WriteAllBytesAsync(Path.Combine(storeDir, "banner.png"), bytes, ct);
                            }
                        }

                        //квадратная обложка 1:1 (BoxArt 1080x1080 или 2160x2160)
                        var boxArtImg = loc.Images
                            .FirstOrDefault(i => (i.ImagePurpose is "BoxArt" or "FeaturePromotionalSquareArt" || (i.Width == 1080 && i.Height == 1080)) && !string.IsNullOrWhiteSpace(i.Uri));

                        if (boxArtImg != null && !string.IsNullOrWhiteSpace(boxArtImg.Uri))
                        {
                            var bytes = await catalogClient.DownloadImageBytesAsync(boxArtImg.Uri, ct);
                            if (bytes != null && bytes.Length > 0)
                            {
                                await File.WriteAllBytesAsync(Path.Combine(storeDir, "boxart.png"), bytes, ct);
                            }
                        }

                        //постер 9:16 (Poster 720x1080 или 1440x2160)
                        var posterImg = loc.Images
                            .FirstOrDefault(i => (i.ImagePurpose is "Poster" || (i.Width == 720 && i.Height == 1080) || (i.Width == 1440 && i.Height == 2160)) && !string.IsNullOrWhiteSpace(i.Uri));

                        if (posterImg != null && !string.IsNullOrWhiteSpace(posterImg.Uri))
                        {
                            var bytes = await catalogClient.DownloadImageBytesAsync(posterImg.Uri, ct);
                            if (bytes != null && bytes.Length > 0)
                            {
                                await File.WriteAllBytesAsync(Path.Combine(storeDir, "poster.png"), bytes, ct);
                            }
                        }

                        //фирменная заглавная иллюстрация xbox (BrandedKeyArt 584x800)
                        var keyArtImg = loc.Images
                            .FirstOrDefault(i => (i.ImagePurpose is "BrandedKeyArt" or "KeyArt" or "XboxBrandedKeyArt" || (i.Width == 584 && i.Height == 800)) && !string.IsNullOrWhiteSpace(i.Uri));

                        if (keyArtImg != null && !string.IsNullOrWhiteSpace(keyArtImg.Uri))
                        {
                            var bytes = await catalogClient.DownloadImageBytesAsync(keyArtImg.Uri, ct);
                            if (bytes != null && bytes.Length > 0)
                            {
                                await File.WriteAllBytesAsync(Path.Combine(storeDir, "branded_key_art.png"), bytes, ct);
                            }
                        }

                        //дополнительные значки-плитки 150x150 и 71x71
                        var icon150Img = loc.Images
                            .FirstOrDefault(i => i.ImagePurpose is "Logo" && i.Width == 150 && i.Height == 150 && !string.IsNullOrWhiteSpace(i.Uri));
                        if (icon150Img != null && !string.IsNullOrWhiteSpace(icon150Img.Uri))
                        {
                            var bytes = await catalogClient.DownloadImageBytesAsync(icon150Img.Uri, ct);
                            if (bytes != null && bytes.Length > 0)
                            {
                                await File.WriteAllBytesAsync(Path.Combine(storeDir, "icon_150.png"), bytes, ct);
                            }
                        }

                        var icon71Img = loc.Images
                            .FirstOrDefault(i => i.ImagePurpose is "Logo" && i.Width == 71 && i.Height == 71 && !string.IsNullOrWhiteSpace(i.Uri));
                        if (icon71Img != null && !string.IsNullOrWhiteSpace(icon71Img.Uri))
                        {
                            var bytes = await catalogClient.DownloadImageBytesAsync(icon71Img.Uri, ct);
                            if (bytes != null && bytes.Length > 0)
                            {
                                await File.WriteAllBytesAsync(Path.Combine(storeDir, "icon_71.png"), bytes, ct);
                            }
                        }

                        var screenshots = loc.Images
                            .Where(i => i.ImagePurpose is "Screenshot" && !string.IsNullOrWhiteSpace(i.Uri))
                            .ToList();

                        if (screenshots.Count > 0)
                        {
                            var screenDir = Path.Combine(storeDir, "screenshots");
                            var urls = screenshots.Select(s => s.Uri!).ToList();
                            await SyncScreenshotsAsync(screenDir, urls, catalogClient.DownloadImageBytesAsync, "{0:D2}_desktop.png", ct);
                        }
                    }

                    if (loc.CMSVideos != null && loc.CMSVideos.Count > 0)
                    {
                        var primaryVideo = loc.CMSVideos.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v.Uri));
                        if (primaryVideo != null && !string.IsNullOrWhiteSpace(primaryVideo.Uri))
                        {
                            var trailerDir = Path.Combine(storeDir, "trailers");
                            _ = Directory.CreateDirectory(trailerDir);
                            var bytes = await catalogClient.DownloadImageBytesAsync(primaryVideo.Uri, ct);
                            if (bytes != null && bytes.Length > 0)
                            {
                                await File.WriteAllBytesAsync(Path.Combine(trailerDir, "trailer.mp4"), bytes, ct);
                            }
                            if (!string.IsNullOrWhiteSpace(primaryVideo.Caption))
                            {
                                await File.WriteAllTextAsync(Path.Combine(trailerDir, "trailer_title.txt"), primaryVideo.Caption.Trim(), Encoding.UTF8, ct);
                            }
                        }
                    }

                    AnsiTerminal.Success($"[Windows/{localeCode}] Сохранены текстовые метаданные и графика.");
                    pulledLocales++;
                }
            }
            catch (Exception ex)
            {
                AnsiTerminal.Warning($"Не удалось получить каталог для рынка {mkt}: {ex.Message}");
            }
        }

        if (pulledLocales == 0 && mstoreCreds != null && !string.IsNullOrWhiteSpace(mstoreCreds.TenantId))
        {
            AnsiTerminal.Info("Попытка импорта через Partner Center Dev Center Ingestion API...");
            try
            {
                var tokenService = new MStoreTokenService();
                var token = await tokenService.GetAccessTokenAsync(mstoreCreds, ct);
                var mstoreClient = new MStoreClient();
                var app = await mstoreClient.GetApplicationAsync(resolvedId, token, ct);
                var subId = app.LastPublishedApplicationSubmission?.Id ?? app.PendingApplicationSubmission?.Id;

                if (!string.IsNullOrWhiteSpace(subId))
                {
                    var sub = await mstoreClient.GetSubmissionAsync(resolvedId, subId, token, ct);
                    if (sub?.Listings != null && sub.Listings.Count > 0)
                    {
                        foreach (var (langKey, listing) in sub.Listings)
                        {
                            var localeCode = LocaleRegistry.NormalizeLocale(langKey);
                            var storeDir = Path.Combine(targetDir, "store", "windows", localeCode);
                            _ = Directory.CreateDirectory(storeDir);

                            if (!string.IsNullOrWhiteSpace(listing.Title))
                            {
                                await File.WriteAllTextAsync(Path.Combine(storeDir, "title.txt"), listing.Title.Trim(), Encoding.UTF8, ct);
                            }
                            if (!string.IsNullOrWhiteSpace(listing.Description))
                            {
                                await File.WriteAllTextAsync(Path.Combine(storeDir, "full_description.txt"), listing.Description.Trim(), Encoding.UTF8, ct);
                            }
                            if (!string.IsNullOrWhiteSpace(listing.ReleaseNotes))
                            {
                                await File.WriteAllTextAsync(Path.Combine(storeDir, "release_notes.txt"), listing.ReleaseNotes.Trim(), Encoding.UTF8, ct);
                            }
                            if (listing.Features != null && listing.Features.Count > 0)
                            {
                                await File.WriteAllTextAsync(Path.Combine(storeDir, "features.txt"), string.Join(Environment.NewLine, listing.Features), Encoding.UTF8, ct);
                            }

                            AnsiTerminal.Success($"[Windows/{localeCode}] Сохранены метаданные из Partner Center.");
                            pulledLocales++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AnsiTerminal.Warning($"Partner Center API импорт завершился ошибкой: {ex.Message}");
            }
        }

        if (pulledLocales == 0)
        {
            AnsiTerminal.Error($"[Windows] Не удалось найти опубликованные метаданные для Product ID '{resolvedId}'.");
            return 1;
        }

        return 0;
    }

    private static async Task<int> PullAndroidAsync(
        string targetDir,
        string? targetId,
        string? configuredPkgName,
        string? serviceAccount,
        string? requestedLang,
        CancellationToken ct)
    {
        var resolvedPkg = !string.IsNullOrWhiteSpace(targetId) && targetId.Contains('.')
            ? targetId
            : configuredPkgName;

        if (string.IsNullOrWhiteSpace(resolvedPkg))
        {
            AnsiTerminal.Error("[Android] Не указан Package Name приложения Google Play. Передайте имя пакета позиционным аргументом или через флаг --package-name.");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(serviceAccount))
        {
            AnsiTerminal.Error("[Android] Не указан сервисный аккаунт Google Play (передайте путь через флаг --service-account или переменную PLAY_CONFIG_JSON).");
            return 1;
        }

        AnsiTerminal.Step("Android", $"Авторизация и чтение метаданных Google Play для '{resolvedPkg}'...");

        string token;
        try
        {
            var sa = GoogleAuthService.ParseServiceAccount(serviceAccount);
            var authService = new GoogleAuthService();
            token = await authService.GetAccessTokenAsync(sa, ct);
        }
        catch (Exception ex)
        {
            AnsiTerminal.Error($"[Android] Ошибка авторизации сервисного аккаунта Google: {ex.Message}");
            return 1;
        }

        var client = new PlayStoreClient();
        string editId;
        try
        {
            editId = await client.CreateEditAsync(resolvedPkg, token, ct);
        }
        catch (Exception ex)
        {
            AnsiTerminal.Error($"[Android] Не удалось создать сессию чтения Google Play edits: {ex.Message}");
            return 1;
        }

        try
        {
            var listings = await client.GetListingsAsync(resolvedPkg, editId, token, ct);
            if (listings.Count == 0)
            {
                AnsiTerminal.Warning("[Android] В Google Play Console не найдено ни одного локализованного описания.");
                return 1;
            }

            //чтение глобальных настроек приложения (privacy, support, website)
            PlayAppDetails? appDetails = null;
            try
            {
                appDetails = await client.GetAppDetailsAsync(resolvedPkg, editId, token, ct);
            }
            catch
            {
            }

            if (!string.IsNullOrWhiteSpace(requestedLang))
            {
                var norm = LocaleRegistry.NormalizeLocale(requestedLang);
                listings = [.. listings.Where(l => string.Equals(LocaleRegistry.NormalizeLocale(l.Language), norm, StringComparison.OrdinalIgnoreCase))];
            }

            var pulledCount = 0;
            foreach (var listing in listings)
            {
                var normLocale = LocaleRegistry.NormalizeLocale(listing.Language);
                var storeDir = Path.Combine(targetDir, "store", "android", normLocale);
                _ = Directory.CreateDirectory(storeDir);

                if (!string.IsNullOrWhiteSpace(listing.Title))
                {
                    await File.WriteAllTextAsync(Path.Combine(storeDir, "title.txt"), listing.Title.Trim(), Encoding.UTF8, ct);
                }
                if (!string.IsNullOrWhiteSpace(listing.ShortDescription))
                {
                    await File.WriteAllTextAsync(Path.Combine(storeDir, "short_description.txt"), listing.ShortDescription.Trim(), Encoding.UTF8, ct);
                }
                if (!string.IsNullOrWhiteSpace(listing.FullDescription))
                {
                    await File.WriteAllTextAsync(Path.Combine(storeDir, "full_description.txt"), listing.FullDescription.Trim(), Encoding.UTF8, ct);
                }

                if (appDetails != null)
                {
                    if (!string.IsNullOrWhiteSpace(appDetails.PrivacyPolicyUrl) &&
                        Uri.TryCreate(appDetails.PrivacyPolicyUrl, UriKind.Absolute, out var pUri) &&
                        (pUri.Scheme == Uri.UriSchemeHttp || pUri.Scheme == Uri.UriSchemeHttps))
                    {
                        await File.WriteAllTextAsync(Path.Combine(storeDir, "privacy.txt"), appDetails.PrivacyPolicyUrl.Trim(), Encoding.UTF8, ct);
                    }
                    if (!string.IsNullOrWhiteSpace(appDetails.ContactEmail))
                    {
                        await File.WriteAllTextAsync(Path.Combine(storeDir, "support_contact.txt"), appDetails.ContactEmail.Trim(), Encoding.UTF8, ct);
                    }
                    if (!string.IsNullOrWhiteSpace(appDetails.ContactWebsite) &&
                        Uri.TryCreate(appDetails.ContactWebsite, UriKind.Absolute, out var wUri) &&
                        (wUri.Scheme == Uri.UriSchemeHttp || wUri.Scheme == Uri.UriSchemeHttps))
                    {
                        await File.WriteAllTextAsync(Path.Combine(storeDir, "website.txt"), appDetails.ContactWebsite.Trim(), Encoding.UTF8, ct);
                    }
                }

                try
                {
                    var icons = await client.GetImagesAsync(resolvedPkg, editId, listing.Language, "icon", token, ct);
                    if (icons.Count > 0 && !string.IsNullOrWhiteSpace(icons[0].Url))
                    {
                        var bytes = await client.DownloadImageBytesAsync(icons[0].Url!, ct);
                        if (bytes != null && bytes.Length > 0)
                        {
                            await File.WriteAllBytesAsync(Path.Combine(storeDir, "icon.png"), bytes, ct);
                        }
                    }

                    var banners = await client.GetImagesAsync(resolvedPkg, editId, listing.Language, "featureGraphic", token, ct);
                    if (banners.Count > 0 && !string.IsNullOrWhiteSpace(banners[0].Url))
                    {
                        var bytes = await client.DownloadImageBytesAsync(banners[0].Url!, ct);
                        if (bytes != null && bytes.Length > 0)
                        {
                            await File.WriteAllBytesAsync(Path.Combine(storeDir, "feature_graphic.png"), bytes, ct);
                        }
                    }

                    var phoneScreenshots = await client.GetImagesAsync(resolvedPkg, editId, listing.Language, "phoneScreenshots", token, ct);
                    if (phoneScreenshots.Count > 0)
                    {
                        var screenDir = Path.Combine(storeDir, "phoneScreenshots");
                        var urls = phoneScreenshots.Where(s => !string.IsNullOrWhiteSpace(s.Url)).Select(s => s.Url!).ToList();
                        await SyncScreenshotsAsync(screenDir, urls, client.DownloadImageBytesAsync, "{0:D2}_screenshot.png", ct);
                    }
                }
                catch (Exception ex)
                {
                    AnsiTerminal.Warning($"[Android/{normLocale}] Ошибка загрузки изображений: {ex.Message}");
                }

                AnsiTerminal.Success($"[Android/{normLocale}] Сохранены текстовые метаданные и графика.");
                pulledCount++;
            }

            return pulledCount > 0 ? 0 : 1;
        }
        finally
        {
            try
            {
                await client.DeleteEditAsync(resolvedPkg, editId, token, ct);
            }
            catch
            {
            }
        }
    }

    private static async Task SyncScreenshotsAsync(
        string screenDir,
        List<string> imageUrls,
        Func<string, CancellationToken, Task<byte[]?>> downloader,
        string defaultPattern,
        CancellationToken ct)
    {
        if (imageUrls.Count == 0)
        {
            return;
        }

        _ = Directory.CreateDirectory(screenDir);

        var existingFiles = Directory.Exists(screenDir)
            ? Directory.EnumerateFiles(screenDir, "*.*")
                .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f)
                .ToList()
            : [];

        for (var idx = 0; idx < imageUrls.Count; idx++)
        {
            var url = imageUrls[idx];
            var bytes = await downloader(url, ct);
            if (bytes == null || bytes.Length == 0)
            {
                continue;
            }

            var prefix = $"{idx + 1:D2}_";
            var existingForSlot = existingFiles.FirstOrDefault(f => Path.GetFileName(f).StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

            var targetFileName = existingForSlot != null
                ? Path.GetFileName(existingForSlot)
                : string.Format(CultureInfo.InvariantCulture, defaultPattern, idx + 1);

            var targetPath = Path.Combine(screenDir, targetFileName);
            await File.WriteAllBytesAsync(targetPath, bytes, ct);

            //чистка возможных дубликатов с тем же порядковым номером
            foreach (var duplicate in existingFiles.Where(f =>
                Path.GetFileName(f).StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(f, targetPath, StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    File.Delete(duplicate);
                }
                catch
                {
                }
            }
        }
    }
}
