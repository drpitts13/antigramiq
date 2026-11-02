using System.Windows;
using System.Windows.Input;
using AntigramIQ.ViewModels;

namespace AntigramIQ.Help;

public static class HelpCommands
{
    public static ICommand ShowTopic { get; } = new RelayCommand(parameter =>
        HelpService.Show(parameter as string));

    public static ICommand ShowContext { get; } = new RelayCommand(parameter =>
        HelpService.Show(HelpLocator.From(parameter as DependencyObject)));
}
