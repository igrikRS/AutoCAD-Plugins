using System;
using Autodesk.AutoCAD.Geometry;

namespace LayoutsFromModel
{
    /// <summary>
    /// Класс, описывающий поведение границ выделенной
    /// области для отображения в пространстве листа
    /// </summary>
    public sealed class DrawingBorders
    {
        private readonly Point3d center;
        private readonly double width;
        private readonly double height;

        /// <summary>
        /// Первая точка рамки
        /// </summary>
        public Point3d First
        {
            get { return GetCorners()[0]; }
        }

        /// <summary>
        /// Вторая точка рамки, противоположная первой
        /// </summary>
        public Point3d Second
        {
            get { return GetCorners()[2]; }
        }

        /// <summary>
        /// Высота выделенной области
        /// </summary>
        public double Height
        {
            get { return height; }
        }

        /// <summary>
        /// Ширина выделенной области
        /// </summary>
        public double Width
        {
            get { return width; }
        }

        /// <summary>
        /// Центр выделенной области
        /// </summary>
        public Point3d Center
        {
            get { return center; }
        }

        /// <summary>
        /// Поворот рамки относительно мировой оси X вокруг оси Z, в радианах.
        /// </summary>
        public double RotationZ { get; private set; }

        string name;
        /// <summary>
        /// Имя области, в дальнейшем имя листа
        /// </summary>
        public string Name
        {
            get { return name; }
            set { name = value; }
        }

        /// <summary>
        /// Масштаб
        /// </summary>
        public double ScaleFactor { get; private set; }

        /// <summary>
        /// Информация о формате бумаги, в который вписываются данные границы
        /// </summary>
        public PlotSettingsInfo PSInfo { get; set; }

        /// <summary>
        /// Создаёт пустое описание границ чертежа.
        /// </summary>
        private DrawingBorders() { }

        /// <summary>
        /// Создаёт описание границ чертежа.
        /// </summary>
        private DrawingBorders(
            Point3d center,
            double width,
            double height,
            double rotationZ,
            string name,
            double scale)
        {
            this.center = center;
            this.width = width;
            this.height = height;
            this.RotationZ = rotationZ;
            this.name = name;
            this.ScaleFactor = scale;
        }

        /// <summary>
        /// Создаёт и инициализирует границы чертежа.
        /// </summary>
        public static DrawingBorders CreateDrawingBorders(Point3d first, Point3d second, string name, double scale)
        {
            Point3d center = new LineSegment3d(first, second).MidPoint;
            DrawingBorders borders = new DrawingBorders(
                center,
                Math.Abs(first.X - second.X),
                Math.Abs(first.Y - second.Y),
                0.0,
                name,
                scale);
            PlotSettingsManager psm = PlotSettingsManager.Current;
            borders.PSInfo = psm.GetPlotSettings(borders, Configuration.AppConfig.Instance.Precision);
            return borders;
        }

        /// <summary>
        /// Создаёт границы прямоугольного чертежа, повёрнутого вокруг оси Z.
        /// </summary>
        public static DrawingBorders CreateRotatedDrawingBorders(
            Point3d center,
            double width,
            double height,
            double rotationZ,
            string name,
            double scale)
        {
            if (width < 0.0)
                throw new ArgumentOutOfRangeException("width");
            if (height < 0.0)
                throw new ArgumentOutOfRangeException("height");

            DrawingBorders borders = new DrawingBorders(
                center,
                width,
                height,
                rotationZ,
                name,
                scale);
            PlotSettingsManager psm = PlotSettingsManager.Current;
            borders.PSInfo = psm.GetPlotSettings(borders, Configuration.AppConfig.Instance.Precision);
            return borders;
        }

        /// <summary>
        /// Возвращает углы рамки по порядку обхода.
        /// </summary>
        public Point3d[] GetCorners()
        {
            double cos = Math.Cos(RotationZ);
            double sin = Math.Sin(RotationZ);
            Vector3d halfWidth = new Vector3d(cos * width / 2.0, sin * width / 2.0, 0.0);
            Vector3d halfHeight = new Vector3d(-sin * height / 2.0, cos * height / 2.0, 0.0);

            return new[]
            {
                center - halfWidth - halfHeight,
                center + halfWidth - halfHeight,
                center + halfWidth + halfHeight,
                center - halfWidth + halfHeight
            };
        }

        /// <summary>
        /// Возвращает текстовое описание границ.
        /// </summary>
        public override string ToString()
        {
            return string.Format(
                "[LayoutBorders Center={0}, Width={1}, Height={2}, RotationZ={3}, Name={4}, Scale={5}]",
                Center,
                Width,
                Height,
                RotationZ,
                name,
                ScaleFactor);
        }

        /// <summary>
        /// Передаёт границы указанному посетителю.
        /// </summary>
        public void Accept(IBorderVisitor visitor)
        {
            visitor.DrawBorder(this);
        }
    }
}
