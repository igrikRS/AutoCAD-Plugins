using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;


namespace Autodesk.AutoCAD.EditorInput
{
    public static class ExtensionMethods
    {
        public static Matrix3d EyeToWorld(this ViewTableRecord view)
        {
            if (view == null)
                throw new ArgumentNullException("ViewTableRecord is null");

            return
                Matrix3d.Rotation(-view.ViewTwist, view.ViewDirection, view.Target) *
                Matrix3d.Displacement(view.Target - Point3d.Origin) *
                Matrix3d.PlaneToWorld(view.ViewDirection);
        }

        public static Matrix3d WorldToEye(this ViewTableRecord view)
        {
            return view.EyeToWorld().Inverse();
        }

        public static void Zoom(this Editor editor, Extents3d ext)
        {
            if (editor == null)
                throw new ArgumentNullException("Editor is null");

            using (ViewTableRecord view = editor.GetCurrentView())
            {
                ext.TransformBy(view.WorldToEye());
                view.Width = ext.MaxPoint.X - ext.MinPoint.X;
                view.Height = ext.MaxPoint.Y - ext.MinPoint.Y;
                view.CenterPoint = new Point2d(
                    (ext.MaxPoint.X + ext.MinPoint.X) / 2.0,
                    (ext.MaxPoint.Y + ext.MinPoint.Y) / 2.0);
                editor.SetCurrentView(view);
            }
        }

        public static void ZoomCenter(this Editor editor, Point3d center, double scale = 1.0)
        {
            if (editor == null)
                throw new ArgumentNullException("Editor is null");

            using (ViewTableRecord view = editor.GetCurrentView())
            {
                center = center.TransformBy(view.WorldToEye());
                view.Height /= scale;
                view.Width /= scale;
                view.CenterPoint = new Point2d(center.X, center.Y);
                editor.SetCurrentView(view);
            }
        }

        public static void ZoomExtents(this Editor editor)
        {
            if (editor == null)
                throw new ArgumentNullException("Editor is null");

            Database db = editor.Document.Database;
            db.UpdateExt(false);
            Extents3d ext = (short)Application.GetSystemVariable("cvport") == 1 ?
                new Extents3d(db.Pextmin, db.Pextmax) :
                new Extents3d(db.Extmin, db.Extmax);
            editor.Zoom(ext);
        }

        public static void ZoomObjects(this Editor editor, IEnumerable<ObjectId> ids)
        {
            if (editor == null)
                throw new ArgumentNullException("Editor is null");

            using (Transaction tr = editor.Document.TransactionManager.StartTransaction())
            {
                Extents3d ext = ids
                    .Where(id => id.ObjectClass.IsDerivedFrom(RXObject.GetClass(typeof(Entity))))
                    .Select(id => ((Entity)tr.GetObject(id, OpenMode.ForRead)).GeometricExtents)
                    .Aggregate((e1, e2) => { e1.AddExtents(e2); return e1; });
                editor.Zoom(ext);
                tr.Commit();
            }
        }

        public static void ZoomScale(this Editor editor, double scale)
        {
            if (editor == null)
                throw new ArgumentNullException("Editor is null");

            using (ViewTableRecord view = editor.GetCurrentView())
            {
                view.Width /= scale;
                view.Height /= scale;
                editor.SetCurrentView(view);
            }
        }

        public static void ZoomWindow(this Editor editor, Point3d p1, Point3d p2)
        {
            using (Line line = new Line(p1, p2))
            {
                editor.Zoom(line.GeometricExtents);
            }
        }
    }
}