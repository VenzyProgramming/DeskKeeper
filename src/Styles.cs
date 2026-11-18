using System.Windows;
using System.Windows.Markup;

namespace DeskKeeper;

/// <summary>Eigene Bedienelemente statt der WPF-Standardoptik (18.11.2026: „keine nativen WPF-Elemente“):
/// schmale Bildlaufleiste ohne Pfeile, die beim Überfahren breiter wird; dunkle, abgerundete Hinweise.</summary>
static class Styles
{
    /// <summary>Implizite Stile für ein Fenster; thumb = Farbe des Griffs (je Ausgabe).</summary>
    public static ResourceDictionary For(string thumb, string thumbHover, string tipFill, string tipLine) => (ResourceDictionary)XamlReader.Parse($$"""
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Style x:Key="Griff" TargetType="Thumb">
    <Setter Property="Template"><Setter.Value>
      <ControlTemplate TargetType="Thumb">
        <Border x:Name="g" Background="{{thumb}}" CornerRadius="3"/>
        <ControlTemplate.Triggers>
          <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="g" Property="Background" Value="{{thumbHover}}"/></Trigger>
          <Trigger Property="IsDragging" Value="True"><Setter TargetName="g" Property="Background" Value="{{thumbHover}}"/></Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value></Setter>
  </Style>
  <Style x:Key="Leer" TargetType="RepeatButton">
    <Setter Property="Focusable" Value="False"/>
    <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="RepeatButton"><Border Background="Transparent"/></ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style TargetType="ScrollBar">
    <Setter Property="Width" Value="12"/>
    <Setter Property="MinWidth" Value="12"/>
    <Setter Property="Template"><Setter.Value>
      <ControlTemplate TargetType="ScrollBar">
        <Grid Background="Transparent">
          <Track x:Name="PART_Track" IsDirectionReversed="True" HorizontalAlignment="Right" Width="4" Margin="0,4,3,4">
            <Track.DecreaseRepeatButton><RepeatButton Style="{StaticResource Leer}" Command="ScrollBar.PageUpCommand"/></Track.DecreaseRepeatButton>
            <Track.IncreaseRepeatButton><RepeatButton Style="{StaticResource Leer}" Command="ScrollBar.PageDownCommand"/></Track.IncreaseRepeatButton>
            <Track.Thumb><Thumb Style="{StaticResource Griff}" MinHeight="28"/></Track.Thumb>
          </Track>
        </Grid>
        <ControlTemplate.Triggers>
          <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="PART_Track" Property="Width" Value="7"/><Setter TargetName="PART_Track" Property="Margin" Value="0,4,2,4"/></Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value></Setter>
  </Style>
  <Style TargetType="ScrollViewer">
    <Setter Property="HorizontalScrollBarVisibility" Value="Disabled"/>
    <Setter Property="Template"><Setter.Value>
      <ControlTemplate TargetType="ScrollViewer">
        <Grid>
          <ScrollContentPresenter Margin="{TemplateBinding Padding}" CanContentScroll="{TemplateBinding CanContentScroll}"/>
          <ScrollBar x:Name="PART_VerticalScrollBar" HorizontalAlignment="Right" Orientation="Vertical"
                     Value="{TemplateBinding VerticalOffset}" Maximum="{TemplateBinding ScrollableHeight}" ViewportSize="{TemplateBinding ViewportHeight}"
                     Visibility="{TemplateBinding ComputedVerticalScrollBarVisibility}"/>
        </Grid>
      </ControlTemplate>
    </Setter.Value></Setter>
  </Style>
  <Style TargetType="ToolTip">
    <Setter Property="OverridesDefaultStyle" Value="True"/>
    <Setter Property="HasDropShadow" Value="False"/>
    <Setter Property="Template"><Setter.Value>
      <ControlTemplate TargetType="ToolTip">
        <Border Background="{{tipFill}}" BorderBrush="{{tipLine}}" BorderThickness="1" CornerRadius="6" Padding="10,6,10,7" Margin="4">
          <ContentPresenter TextElement.Foreground="#F0F0F0" TextElement.FontSize="12.5"/>
        </Border>
      </ControlTemplate>
    </Setter.Value></Setter>
  </Style>
</ResourceDictionary>
""");
}
