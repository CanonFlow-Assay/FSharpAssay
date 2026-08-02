namespace FsAssay.Desktop

open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Markup.Xaml

[<System.Diagnostics.CodeAnalysis.SuppressMessage("FsAssay", "FSA-F04")>]
type App() =
    inherit Application()

    override this.Initialize() =
            AvaloniaXamlLoader.Load(this) // EXPECT: FSA-P02

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with // EXPECT: FSA-F04
        | :? IClassicDesktopStyleApplicationLifetime as desktop ->
             desktop.MainWindow <- MainWindow()
        | _ -> ()

        base.OnFrameworkInitializationCompleted()
