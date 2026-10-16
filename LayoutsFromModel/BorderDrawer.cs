using System;
using System.Collections.Generic;

using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using ADS = Autodesk.AutoCAD.DatabaseServices;

namespace LayoutsFromModel
{
	/// <summary>
	/// Посетитель, отрисовывающий временной графикой выбранные пользователем рамки.
	/// </summary>
	public class BorderDrawer : IBorderVisitor
	{
		private readonly List<Drawable> objects = new List<Drawable>();
		private readonly Color graphicsColor = Color.FromColorIndex(ColorMethod.ByLayer, 20);
		private readonly IntegerCollection viewportNumbers = new IntegerCollection();
		private readonly Editor ed;

		public BorderDrawer(Editor editor)
		{
			this.ed = editor;
		}
		
		/// <summary>
		/// Рисует границу и имя чертежа.
		/// </summary>
		public void DrawBorder(DrawingBorders border)
		{
			try
			{
				// Обводим рамку прямоугольником
				Drawable rec = CreateRectangle(border.First, border.Second);
				AddTransient(rec, TransientDrawingMode.Highlight, 128);

				// Вписываем в рамку название будущего листа и его формат
				Drawable txt = CreateLayoutNameMText(border.Center, border.Name, border.PSInfo.Name, border.ScaleFactor);
				AddTransient(txt, TransientDrawingMode.DirectShortTerm, 256);
			}
			catch
			{
				ClearData();
				throw;
			}
		}

		/// <summary>
		/// Регистрирует объект в менеджере временной графики и сохраняет его
		/// только после подтверждённого добавления.
		/// </summary>
		private void AddTransient(Drawable drawable, TransientDrawingMode mode, int subDrawingMode)
		{
			try
			{
				TransientManager tm = TransientManager.CurrentTransientManager;
				if (tm == null || !tm.AddTransient(drawable, mode, subDrawingMode, this.viewportNumbers))
				{
					drawable.Dispose();
					return;
				}

				this.objects.Add(drawable);
			}
			catch
			{
				// AddTransient мог зарегистрировать объект до возникновения исключения.
				// Не допускаем его последующую финализацию с неизвестным состоянием менеджера.
				GC.SuppressFinalize(drawable);
				throw;
			}
		}
		
		/// <summary>
		/// Создаёт прямоугольник по двум точкам.
		/// </summary>
		private ADS.DBObject CreateRectangle(Point3d first, Point3d second)
		{
			ADS.Polyline pl = new ADS.Polyline(4);
			pl.Color = this.graphicsColor;
			double firstX = first.X;
			double secondX = second.X;
			double firstY = first.Y;
			double secondY = second.Y;
			pl.AddVertexAt(0, new Point2d(firstX, firstY), 0, 0, 0);
			pl.AddVertexAt(1, new Point2d(firstX, secondY), 0, 0, 0);
			pl.AddVertexAt(2, new Point2d(secondX, secondY), 0, 0, 0);
			pl.AddVertexAt(3, new Point2d(secondX, firstY), 0, 0, 0);
			pl.Closed = true;
			return pl;
		}
		
		/// <summary>
		/// Создаёт текст с именем листа.
		/// </summary>
		private ADS.DBObject CreateLayoutNameMText(Point3d center, string name, string format, double scaleFactor)
		{
			ADS.MText mt = new ADS.MText();
			mt.SetDatabaseDefaults(ADS.HostApplicationServices.WorkingDatabase);
			mt.BackgroundFillColor = this.graphicsColor;
			mt.BackgroundFill = true;
			mt.Contents = name + "\\P" + format;
			mt.TextHeight = 12 * scaleFactor;
			mt.Location = center;
			mt.Attachment = ADS.AttachmentPoint.MiddleCenter;
			return mt;
		}
		
		/// <summary>
		/// Очищает созданные графические объекты.
		/// </summary>
		public void ClearData()
		{
			if (this.objects.Count == 0)
				return;

			bool fallbackHandled = false;
			TransientManager tm = TransientManager.CurrentTransientManager;
			foreach (Drawable obj in this.objects)
			{
				bool erased = false;
				try
				{
					erased = tm != null && tm.EraseTransient(obj, this.viewportNumbers);
				}
				catch
				{
					// Ошибка очистки временной графики не должна прерывать отмену команды.
					fallbackHandled = true;
				}

				if (erased)
				{
					try
					{
						obj.Dispose();
					}
					catch
					{
						fallbackHandled = true;
						GC.SuppressFinalize(obj);
					}
				}
				else
				{
					fallbackHandled = true;
					// AutoCAD всё ещё может хранить нативный указатель на объект.
					// Освобождение или финализация в этом состоянии приводит
					// к Access Violation и критическому вылету из AutoCAD.
					// Во избежании краша - выполняем через GC.SuppressFinalize(obj)
					GC.SuppressFinalize(obj);
				}
			}

			this.objects.Clear();

			if (fallbackHandled && this.ed != null)
			{
				try
				{
					this.ed.WriteMessage(
						"\nПредупреждение: обработан аварийный сценарий очистки временной графики.\n");
				}
				catch
				{
					// Диагностический вывод не должен влиять на выполнение команды.
				}
			}
		}
	}
}
