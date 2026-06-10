using System.Windows;
using System.Windows.Controls;

namespace CaForecast.WpfApp;

public static class PasswordBoxBinding
{
    public static readonly DependencyProperty BoundPasswordProperty =
        DependencyProperty.RegisterAttached(
            "BoundPassword",
            typeof(string),
            typeof(PasswordBoxBinding),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBoundPasswordChanged));

    private static readonly DependencyProperty IsUpdatingProperty =
        DependencyProperty.RegisterAttached(
            "IsUpdating",
            typeof(bool),
            typeof(PasswordBoxBinding));

    public static string GetBoundPassword(DependencyObject element)
    {
        return (string)element.GetValue(BoundPasswordProperty);
    }

    public static void SetBoundPassword(DependencyObject element, string value)
    {
        element.SetValue(BoundPasswordProperty, value);
    }

    private static bool GetIsUpdating(DependencyObject element)
    {
        return (bool)element.GetValue(IsUpdatingProperty);
    }

    private static void SetIsUpdating(DependencyObject element, bool value)
    {
        element.SetValue(IsUpdatingProperty, value);
    }

    private static void OnBoundPasswordChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (element is not PasswordBox passwordBox)
        {
            return;
        }

        passwordBox.PasswordChanged -= OnPasswordChanged;
        if (!GetIsUpdating(passwordBox))
        {
            passwordBox.Password = (string?)args.NewValue ?? string.Empty;
        }

        passwordBox.PasswordChanged += OnPasswordChanged;
    }

    private static void OnPasswordChanged(object sender, RoutedEventArgs args)
    {
        if (sender is not PasswordBox passwordBox)
        {
            return;
        }

        SetIsUpdating(passwordBox, true);
        SetBoundPassword(passwordBox, passwordBox.Password);
        SetIsUpdating(passwordBox, false);
    }
}
