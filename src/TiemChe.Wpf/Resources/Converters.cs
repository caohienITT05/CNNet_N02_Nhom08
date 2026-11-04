using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using TiemChe.Wpf.Services;

namespace TiemChe.Wpf.Resources;

public sealed class DisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => parameter switch
    {
        "Money" => value is long amount ? Display.Money(amount) : "—",
        "Date" => value is DateTime date ? Display.LocalTime(date).ToString("dd/MM/yyyy HH:mm", Display.Vietnamese) : "—",
        "Visible" => value is not null && (value is not string text || !string.IsNullOrWhiteSpace(text))
            ? Visibility.Visible : Visibility.Collapsed,
        "Inverse" => value is not true,
        "Image" => value is string path && path.StartsWith("/images/", StringComparison.Ordinal)
            ? new Uri(ApiConfiguration.FromEnvironment().BaseAddress, path.TrimStart('/')) : null,
        "Qr" => value is string url && Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps && uri.Host == "img.vietqr.io" ? uri : null,
        _ => Display.Label(value)
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public static class PasswordBinding
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.RegisterAttached("Value", typeof(string),
        typeof(PasswordBinding), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnValueChanged));

    private static readonly DependencyProperty UpdatingProperty =
        DependencyProperty.RegisterAttached("Updating", typeof(bool), typeof(PasswordBinding));

    public static string GetValue(DependencyObject target) => (string)target.GetValue(ValueProperty);
    public static void SetValue(DependencyObject target, string value) => target.SetValue(ValueProperty, value);

    private static void OnValueChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not PasswordBox box || (bool)box.GetValue(UpdatingProperty))
            return;
        box.PasswordChanged -= OnPasswordChanged;
        box.Password = (string?)args.NewValue ?? "";
        box.PasswordChanged += OnPasswordChanged;
    }

    private static void OnPasswordChanged(object sender, RoutedEventArgs args)
    {
        var box = (PasswordBox)sender;
        box.SetValue(UpdatingProperty, true);
        box.SetCurrentValue(ValueProperty, box.Password);
        box.SetValue(UpdatingProperty, false);
    }
}
