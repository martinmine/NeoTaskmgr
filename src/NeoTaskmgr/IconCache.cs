using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace NeoTaskmgr;

/// <summary>
/// Icons of executables, loaded once per path via the shell's thumbnails, logos of packaged apps, and pictures. For use from
/// x:Bind on the UI thread.
/// </summary>
public static class IconCache
{
    private const uint IconSize = 32; // Rendered at 16 px; 32 keeps it sharp at 200% scaling.
    private static readonly Dictionary<string, BitmapImage> Icons = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns the icon for the file; it appears once loaded. Null when there is no path.</summary>
    public static ImageSource? Get(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        if (!Icons.TryGetValue(path, out var icon))
        {
            icon = new BitmapImage { DecodePixelWidth = (int)IconSize };
            Icons[path] = icon;
            _ = LoadAsync(icon, path);
        }

        return icon;
    }

    /// <summary>The logo of a packaged app, or the icon of a program; null when neither is known.</summary>
    public static ImageSource? GetApp(string? path, string? packageFamilyName)
    {
        if (packageFamilyName is null)
        {
            return Get(path);
        }

        var key = "package:" + packageFamilyName;
        if (!Icons.TryGetValue(key, out var icon))
        {
            icon = new BitmapImage { DecodePixelWidth = (int)IconSize };
            Icons[key] = icon;
            _ = LoadPackageLogoAsync(icon, packageFamilyName);
        }

        return icon;
    }

    /// <summary>An image file shown as is, such as an account picture; it appears once loaded. Null when there is no path.</summary>
    public static ImageSource? GetPicture(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        var key = "picture:" + path;
        if (!Icons.TryGetValue(key, out var picture))
        {
            picture = new BitmapImage { DecodePixelWidth = (int)IconSize };
            Icons[key] = picture;
            _ = LoadPictureAsync(picture, path);
        }

        return picture;
    }

    private static async Task LoadPictureAsync(BitmapImage picture, string path)
    {
        try
        {
            // Read directly: account pictures are hidden system files, which the shell's StorageFile does not open.
            var bytes = await File.ReadAllBytesAsync(path);
            using var stream = new MemoryStream(bytes);
            await picture.SetSourceAsync(stream.AsRandomAccessStream());
        }
        catch (Exception)
        {
            // As for icons: a picture that cannot be read is cosmetic.
        }
    }

    private static async Task LoadPackageLogoAsync(BitmapImage icon, string familyName)
    {
        try
        {
            if (await Task.Run(() => AppPackages.GetEntry(familyName)) is { } entry)
            {
                using var stream = await entry.DisplayInfo.GetLogo(new Windows.Foundation.Size(IconSize, IconSize)).OpenReadAsync();
                await icon.SetSourceAsync(stream);
            }
        }
        catch (Exception)
        {
            // As for files: a missing logo is cosmetic.
        }
    }

    private static async Task LoadAsync(BitmapImage icon, string path)
    {
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            using var thumbnail = await GetThumbnailAsync(file);
            if (thumbnail is not null)
            {
                await icon.SetSourceAsync(thumbnail);
            }
        }
        catch (Exception)
        {
            // An icon is cosmetic: files that cannot be read (access denied, deleted) simply show no icon.
        }
    }

    private static async Task<StorageItemThumbnail?> GetThumbnailAsync(StorageFile file)
    {
        // While the shell is still extracting an icon that is not in its thumbnail cache yet, a request fails with E_PENDING.
        // This happens when many icons are requested at once, as when the Processes page first loads; asking again shortly
        // after succeeds.
        const int EPending = unchecked((int)0x8000000A);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await file.GetThumbnailAsync(ThumbnailMode.SingleItem, IconSize, ThumbnailOptions.ResizeThumbnail);
            }
            catch (Exception e) when (e.HResult == EPending && attempt < 5)
            {
                await Task.Delay(200 * attempt);
            }
        }
    }
}
