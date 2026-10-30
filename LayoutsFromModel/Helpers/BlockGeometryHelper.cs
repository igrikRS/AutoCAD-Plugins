using Autodesk.AutoCAD.DatabaseServices;

namespace LayoutsFromModel
{
    /// <summary>
    /// Вспомогательные методы для определения границ геометрии блока.
    /// </summary>
    internal static class BlockGeometryHelper
    {
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
    }
}
