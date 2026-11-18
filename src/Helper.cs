using System.Windows;
using System.Windows.Media;

namespace DeskKeeper;

/// <summary>Helfer, der die Symbole zurückbringt (derzeit nur Дубина).</summary>
interface IHelper
{
    double End { get; }
    int Carried { get; }
    (bool Handled, Point? At) IconAt(string id, double t);
    void Draw(DrawingContext dc, double t);
}
