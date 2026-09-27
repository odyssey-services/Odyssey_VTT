namespace Odyssey.Unity.Client
{
    /// <summary>
    /// ODY-S08-103: the single point of contact with the vendored third-party native-file-dialog library
    /// (`gkngkc/UnityStandaloneFileBrowser`, MIT, vendored under <c>ThirdParty/StandaloneFileBrowser/</c>).
    /// No other type in this codebase references the <c>SFB</c> namespace directly -- if the library is
    /// ever replaced, this is the only file that changes.
    /// </summary>
    public static class NativeFileDialog
    {
        /// <summary>
        /// Opens a native "Open File" dialog restricted to PNG/JPEG images. Returns the chosen absolute
        /// path, or <c>null</c> if the user cancelled (the underlying library returns an empty array on
        /// cancel; this wrapper turns that into <c>null</c> so a caller never has to know the library's own
        /// empty-array-means-cancelled convention).
        /// </summary>
        public static string? OpenImageFile(string title)
        {
            var extensions = new[] { new SFB.ExtensionFilter("Images", "png", "jpg", "jpeg") };
            string[] paths = SFB.StandaloneFileBrowser.OpenFilePanel(title, string.Empty, extensions, false);
            return paths.Length > 0 && !string.IsNullOrWhiteSpace(paths[0]) ? paths[0] : null;
        }
    }
}
