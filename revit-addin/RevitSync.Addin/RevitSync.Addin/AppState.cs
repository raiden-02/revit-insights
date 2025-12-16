namespace RevitSync.Addin
{
    public static class AppState
    {
        // Safe to read from background threads (string reference assignment is atomic)
        public static volatile string ActiveProjectName = "";
    }
}

