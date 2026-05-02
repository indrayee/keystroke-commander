using System;
using System.ComponentModel;
using System.Windows;

namespace KeystrokeCommander;

public partial class MainWindow : Window
{
    private readonly ViewModels.MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = (ViewModels.MainViewModel)DataContext;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        _vm.InitHotkeys(helper.Handle);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _vm.Cleanup();
        base.OnClosing(e);
    }
}
