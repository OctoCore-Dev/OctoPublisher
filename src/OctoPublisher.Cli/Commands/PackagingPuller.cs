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
                    var localeCode = !string.IsNullOrWhiteSpace(loc.Language)
                        ? LocaleRegistry.NormalizeLocale(loc.Language)
                        : lng;

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

                    var privacyUrl = loc.PrivacyUrl ?? loc.SupportUri;
                    if (!string.IsNullOrWhiteSpace(privacyUrl))
                    {
                        await File.WriteAllTextAsync(Path.Combine(storeDir, "privacy.txt"), privacyUrl.Trim(), Encoding.UTF8, ct);
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

                        var screenshots = loc.Images
                            .Where(i => i.ImagePurpose is "Screenshot" && !string.IsNullOrWhiteSpace(i.Uri))
                            .ToList();

                        if (screenshots.Count > 0)
                        {
                            var screenDir = Path.Combine(storeDir, "screenshots");
                            _ = Directory.CreateDirectory(screenDir);

                            for (var idx = 0; idx < screenshots.Count; idx++)
                            {
                                var sc = screenshots[idx];
                                var bytes = await catalogClient.DownloadImageBytesAsync(sc.Uri!, ct);
                                if (bytes != null && bytes.Length > 0)
                                {
                                    var screenName = $"{idx + 1:D2}_screenshot.png";
                                    await File.WriteAllBytesAsync(Path.Combine(screenDir, screenName), bytes, ct);
                                }
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
                        _ = Directory.CreateDirectory(screenDir);

                        for (var idx = 0; idx < phoneScreenshots.Count; idx++)
                        {
                            var sc = phoneScreenshots[idx];
                            if (!string.IsNullOrWhiteSpace(sc.Url))
                            {
                                var bytes = await client.DownloadImageBytesAsync(sc.Url!, ct);
                                if (bytes != null && bytes.Length > 0)
                                {
                                    await File.WriteAllBytesAsync(Path.Combine(screenDir, $"{idx + 1:D2}_screenshot.png"), bytes, ct);
                                }
                            }
                        }
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
}
