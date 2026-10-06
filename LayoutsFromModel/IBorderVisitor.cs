using System;

namespace LayoutsFromModel
{
	/// <summary>
	/// Интерфейс посетителя, отрисовывающего границы чертежей
	/// </summary>
	public interface IBorderVisitor
	{
		/// <summary>
		/// Рисует указанные границы чертежа.
		/// </summary>
		void DrawBorder(DrawingBorders border);

		/// <summary>
		/// Очищает созданные данные.
		/// </summary>
		void ClearData();
	}
}
