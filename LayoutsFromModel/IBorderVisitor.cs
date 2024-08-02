using System;

namespace LayoutsFromModel
{
	/// <summary>
	/// Интерфейс посетителя, отрисовывающего границы чертежей
	/// </summary>
	public interface IBorderVisitor
	{
		void DrawBorder(DrawingBorders border);
		void ClearData();
	}
}
