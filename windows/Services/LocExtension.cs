using System;
using System.Windows.Data;
using System.Windows.Markup;

namespace Aenv;

/// <summary>XAML: {l:T key} — returns a binding that refreshes when the language changes.</summary>
public class TExtension : MarkupExtension
{
    public TExtension() { }
    public TExtension(string key) { Key = key; }

    [ConstructorArgument("key")]
    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]") { Source = Loc.I, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}
