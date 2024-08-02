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

        PromptResultStatus queryStatus;
        public PromptResultStatus QueryStatus
        {
            get { return queryStatus; }
            set { queryStatus = value; }
        }

        public BorderPromptResult(Point3d firstPoint, Point3d secondPoint, double scale = 100)
        {
            this.queryStatus = PromptResultStatus.OK;
            this.firstPoint = firstPoint;
            this.secondPoint = secondPoint;
            this.stringResult = "";
            this.scale = scale;
        }

        public BorderPromptResult(string stringResult)
        {
            this.queryStatus = PromptResultStatus.Keyword;
            this.stringResult = stringResult;
            this.firstPoint = Point3d.Origin;
            this.secondPoint = Point3d.Origin;
            this.scale = 100;
        }

        public BorderPromptResult(PromptResultStatus queryStatus, double scale = 100)
        {
            this.queryStatus = queryStatus;
            this.firstPoint = Point3d.Origin;
            this.secondPoint = Point3d.Origin;
            this.stringResult = "";
            this.scale = scale;
        }
    }
}