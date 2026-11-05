using System;
using Autodesk.AutoCAD.Geometry;

namespace LayoutsFromModel
{
    /// <summary>
    /// Результат ввода пользователем рамки чертежа
    /// </summary>
    public struct BorderPromptResult
    {
        Point3d firstPoint;
        public Point3d FirstPoint
        {
            get { return firstPoint; }
        }

        Point3d secondPoint;
        public Point3d SecondPoint
        {
            get { return secondPoint; }
        }

        string stringResult;
        public string StringResult
        {
            get { return stringResult; }
            set { stringResult = value; }
        }

        double scale;
        public double GetScale
        {
            get { return scale; }
        }

        double width;
        public double Width
        {
            get { return width; }
        }

        double height;
        public double Height
        {
            get { return height; }
        }

        double rotationZ;
        public double RotationZ
        {
            get { return rotationZ; }
        }

        bool isRotated;
        public bool IsRotated
        {
            get { return isRotated; }
        }

        PromptResultStatus queryStatus;
        public PromptResultStatus QueryStatus
        {
            get { return queryStatus; }
            set { queryStatus = value; }
        }

        /// <summary>
        /// Создаёт результат выбора двух точек рамки.
        /// </summary>
        public BorderPromptResult(Point3d firstPoint, Point3d secondPoint, double scale = 100)
        {
            this.queryStatus = PromptResultStatus.OK;
            this.firstPoint = firstPoint;
            this.secondPoint = secondPoint;
            this.stringResult = "";
            this.scale = scale;
            this.width = Math.Abs(firstPoint.X - secondPoint.X);
            this.height = Math.Abs(firstPoint.Y - secondPoint.Y);
            this.rotationZ = 0.0;
            this.isRotated = false;
        }

        /// <summary>
        /// Создаёт результат выбора повёрнутой рамки.
        /// </summary>
        public BorderPromptResult(
            Point3d center,
            double width,
            double height,
            double rotationZ,
            double scale)
        {
            this.queryStatus = PromptResultStatus.OK;
            this.firstPoint = center;
            this.secondPoint = center;
            this.stringResult = "";
            this.scale = scale;
            this.width = width;
            this.height = height;
            this.rotationZ = rotationZ;
            this.isRotated = true;
        }

        /// <summary>
        /// Создаёт результат выбора по строковому значению.
        /// </summary>
        public BorderPromptResult(string stringResult)
        {
            this.queryStatus = PromptResultStatus.Keyword;
            this.stringResult = stringResult;
            this.firstPoint = Point3d.Origin;
            this.secondPoint = Point3d.Origin;
            this.scale = 100;
            this.width = 0.0;
            this.height = 0.0;
            this.rotationZ = 0.0;
            this.isRotated = false;
        }

        /// <summary>
        /// Создаёт результат с указанным состоянием запроса.
        /// </summary>
        public BorderPromptResult(PromptResultStatus queryStatus, double scale = 100)
        {
            this.queryStatus = queryStatus;
            this.firstPoint = Point3d.Origin;
            this.secondPoint = Point3d.Origin;
            this.stringResult = "";
            this.scale = scale;
            this.width = 0.0;
            this.height = 0.0;
            this.rotationZ = 0.0;
            this.isRotated = false;
        }
    }
}
