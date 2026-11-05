using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace LayoutsFromModel
{
    /// <summary>
    /// Вспомогательные методы для определения границ геометрии блока.
    /// </summary>
    internal static class BlockGeometryHelper
    {
        internal struct OrientedBlockBounds
        {
            public OrientedBlockBounds(Point3d center, double width, double height, double rotationZ)
            {
                Center = center;
                Width = width;
                Height = height;
                RotationZ = rotationZ;
            }

            public Point3d Center { get; private set; }
            public double Width { get; private set; }
            public double Height { get; private set; }
            public double RotationZ { get; private set; }
        }

        /// <summary>
        /// Возвращает общие границы видимых элементов вхождения блока.
        /// </summary>
        public static Extents3d GetVisibleExtents(BlockReference blockReference)
        {
            Extents3d extents = new Extents3d();
            using (DBObjectCollection entities = new DBObjectCollection())
            {
                blockReference.Explode(entities);
                foreach (Entity entity in entities)
                {
                    try
                    {
                        if (!entity.Visible)
                            continue;

                        Extents3d? bounds = entity.Bounds;
                        if (bounds.HasValue)
                            extents.AddExtents(bounds.Value);
                    }
                    finally
                    {
                        entity.Dispose();
                    }
                }
            }

            return extents;
        }

        /// <summary>
        /// Возвращает границы видимых элементов в системе координат,
        /// повёрнутой вместе с вхождением блока вокруг мировой оси Z.
        /// </summary>
        public static OrientedBlockBounds GetVisibleOrientedBounds(BlockReference blockReference)
        {
            double rotation = blockReference.Rotation;
            Matrix3d toAligned = Matrix3d.Rotation(
                -rotation,
                Vector3d.ZAxis,
                blockReference.Position);

            Extents3d? extents = null;
            using (DBObjectCollection entities = new DBObjectCollection())
            {
                blockReference.Explode(entities);
                foreach (Entity entity in entities)
                {
                    try
                    {
                        if (!entity.Visible)
                            continue;

                        entity.TransformBy(toAligned);
                        Extents3d? bounds = entity.Bounds;
                        if (!bounds.HasValue)
                            continue;

                        if (extents.HasValue)
                        {
                            Extents3d combined = extents.Value;
                            combined.AddExtents(bounds.Value);
                            extents = combined;
                        }
                        else
                        {
                            extents = bounds.Value;
                        }
                    }
                    finally
                    {
                        entity.Dispose();
                    }
                }
            }

            if (!extents.HasValue)
                throw new InvalidOperationException("Не удалось определить видимые границы блока.");

            Extents3d aligned = extents.Value;
            Point3d alignedCenter = new LineSegment3d(
                aligned.MinPoint,
                aligned.MaxPoint).MidPoint;
            Point3d center = alignedCenter.TransformBy(toAligned.Inverse());

            return new OrientedBlockBounds(
                center,
                aligned.MaxPoint.X - aligned.MinPoint.X,
                aligned.MaxPoint.Y - aligned.MinPoint.Y,
                rotation);
        }
    }
}
