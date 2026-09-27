namespace IntegrationHub.Web;

// Original 24px vector symbols with one stroke weight, independent of icon fonts or CDNs.
// Only these constant paths are inserted as SVG markup; authored content never enters this method.
public static class HubIcons
{
    private static string Stroke(params string[] paths) => string.Concat(paths.Select(path =>
        $"<path d=\"{path}\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.7\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>"));

    public static readonly string SpaceDashboard = Stroke("M3 3h7v7H3zM14 3h7v11h-7zM3 14h7v7H3zM14 18h7v3h-7z");
    public static readonly string AccountTree = Stroke("M3 8h6v8H3zM15 2h6v6h-6zM15 14h6v8h-6zM9 12h3V5h3M12 12v6h3");
    public static readonly string AutoGraph = Stroke("M2 3h6v6H2zM16 15h6v6h-6zM5 9v8a2 2 0 0 0 2 2h9M16 4h6m-3-3v6M10 7l3 3-3 3");
    public static readonly string Widgets = Stroke("M3 3h7v7H3zM14 3h7v7h-7zM3 14h7v7H3zM14 14h7v7h-7z");
    public static readonly string Dns = Stroke("M3 3h18v7H3zM3 14h18v7H3zM7 6.5h.01M7 17.5h.01M12 6.5h5M12 17.5h5");
    public static readonly string Forum = Stroke("M4 4h16v12H9l-5 4zM8 8h8M8 12h5");
    public static readonly string Hub = Stroke("M9 9h6v6H9zM2 2h4v4H2zM18 2h4v4h-4zM2 18h4v4H2zM18 18h4v4h-4zM6 6l3 3M15 9l3-3M6 18l3-3M15 15l3 3");
    public static readonly string Search = Stroke("M18 10a7 7 0 1 1-14 0 7 7 0 1 1 14 0M15 15l6 6");
    public static readonly string UploadFile = Stroke("M12 16V3m-4 4 4-4 4 4M4 14v6h16v-6");
    public static readonly string Download = Stroke("M12 3v13m-4-4 4 4 4-4M4 16v5h16v-5");
    public static readonly string Add = Stroke("M12 5v14M5 12h14");
    public static readonly string Minus = Stroke("M5 12h14");
    public static readonly string Code = Stroke("m8 6-6 6 6 6m8-12 6 6-6 6m-3-15-2 18");
    public static readonly string Gateway = Stroke("M4 5h16v14H4zM8 5v14M16 5v14M1 12h6m10 0h6");
    public static readonly string Bolt = Stroke("m14 2-10 12h7l-1 8L20 9h-7z");
    public static readonly string LogicApp = Stroke("M3 3h6v6H3zM15 15h6v6h-6zM15 3h6v6h-6zM9 6h6M6 9v9h9");
    public static readonly string Topic = Stroke("M2 9h6v6H2zM16 2h6v6h-6zM16 16h6v6h-6zM8 12h4V5h4m-4 7v7h4");
    public static readonly string Queue = Stroke("M2 5h20v14H2zM7 9v6m5-6v6m5-6v6");
    public static readonly string Event = Stroke("m12 2 10 10-10 10L2 12zM8 12h8m-4-4v8");
    public static readonly string Database = Stroke("M3 6c0-5 18-5 18 0s-18 5-18 0v12c0 5 18 5 18 0V6M3 12c0 5 18 5 18 0");
    public static readonly string Storage = Stroke("M3 5h18v15H3zM2 5V2h20v3M9 10h6");
    public static readonly string CloudQueue = Stroke("M7 18a5 5 0 0 1-1-10 6 6 0 0 1 11-2 6 6 0 0 1 1 12z");
    public static readonly string Package = Stroke("m12 2 10 5v10l-10 5-10-5V7zM2 7l10 5 10-5M12 12v10M7 4l10 5");
    public static readonly string Library = Stroke("M3 3h4v18H3zM10 3h4v18h-4zM16 4l4-1 3 17-4 1z");
    public static readonly string Transform = Stroke("M3 6h14m-4-4 4 4-4 4M21 18H7m4-4-4 4 4 4");
    public static readonly string File = Stroke("M5 2h9l5 5v15H5zM14 2v6h5M8 12h8M8 16h8");
    public static readonly string Sftp = Stroke("M4 2h9l5 5v5M13 2v6h5M4 2v20h7M12 16h10m-3-3 3 3-3 3M16 22h-4m2-2-2 2");
    public static readonly string FileShare = Stroke("M3 4h6l2 3h10v10H3zM12 17v4M7 21h10");
    public static readonly string Person = Stroke("M8 6a4 4 0 1 0 8 0 4 4 0 1 0-8 0M4 22v-4a8 8 0 0 1 16 0v4");
    public static readonly string InternalSystem = Stroke("M4 4h16v12H4zM8 20h8M12 16v4M7 8h4M7 11h7");
    public static readonly string ExternalSystem = Stroke("M21 12a9 9 0 1 1-18 0 9 9 0 1 1 18 0M3 12h18M12 3c-5 4-5 14 0 18 5-4 5-14 0-18");
    public static readonly string Custom = Stroke("m12 2 9 5v10l-9 5-9-5V7zM8 12h8M12 8v8");
    public static readonly string Rule = Stroke("M5 3h14v18H5zM8 8l1 1 2-2M13 8h3M8 14l1 1 2-2M13 14h3");
    public static readonly string FactCheck = Rule;
    public static readonly string GridView = Widgets;
    public static readonly string TableRows = Stroke("M3 4h18v16H3zM3 9h18M3 14h18M9 4v16");
    public static readonly string Save = Stroke("M4 3h13l4 4v14H3V3zM7 3v6h10V3M7 21v-8h10v8");
    public static readonly string Edit = Stroke("m16 3 5 5-12 12-6 1 1-6zM13 6l5 5");
    public static readonly string CheckCircle = Stroke("M21 12a9 9 0 1 1-18 0 9 9 0 1 1 18 0M8 12l3 3 5-6");
    public static readonly string PriorityHigh = Stroke("M12 3v11M12 19v1");
    public static readonly string Refresh = Stroke("M20 8a9 9 0 0 0-15-3L2 8m0-5v5h5M4 16a9 9 0 0 0 15 3l3-3m0 5v-5h-5");
    public static readonly string ArrowRight = Stroke("M4 12h16m-6-6 6 6-6 6");
    public static readonly string ArrowOut = Stroke("M6 18 18 6M6 6h12v12");
    public static readonly string Send = Stroke("m3 3 19 9-19 9 4-9zM7 12h15");
    public static readonly string Receive = Stroke("M12 2v12m-4-4 4 4 4-4M3 14v7h18v-7");
    public static readonly string Fit = Stroke("M3 9V3h6M15 3h6v6M21 15v6h-6M9 21H3v-6M8 8h8v8H8z");
    public static readonly string Fullscreen = Stroke("M3 9V3h6M15 3h6v6M21 15v6h-6M9 21H3v-6");
    public static readonly string Filter = Stroke("M3 5h18M6 12h12M10 19h4M8 3v4M16 10v4M12 17v4");
}
