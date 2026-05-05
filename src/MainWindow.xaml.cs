using System.Windows;
using KeystrokeCommander.ViewModels;

namespace KeystrokeCommander;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.InitHotkeys(new System.Windows.Interop.WindowInteropHelper(this).Handle);
                vm.CommitEditAction = () => EditorGrid.CommitEdit();
            }
        };
        Closing += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
                vm.Cleanup();
        };
    }

    private void WindowPicker_DropDownOpened(object sender, System.EventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.WindowPickerIsOpen = true;
    }

    private void WindowPicker_DropDownClosed(object sender, System.EventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.WindowPickerIsOpen = false;
    }
}
