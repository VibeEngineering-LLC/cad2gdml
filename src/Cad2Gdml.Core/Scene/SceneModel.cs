using System;
using System.Collections.Generic;
using System.Globalization;
using Cad2Gdml.Meshes;

namespace Cad2Gdml.Scene
{
    // Роль детали в сцене
    public enum PartRole
    {
        Other,
        Crystal,
        Source,
        Container,
        Shield,
        Housing
    }

    // Уровень серьёзности диагностики
    public enum DiagSeverity
    {
        Info,
        Warning,
        Error
    }

    // Строка диагностики
    public sealed class Diagnostic
    {
        public string Code { get; }
        public DiagSeverity Severity { get; }
        public string PartName { get; }
        public string Message { get; }

        public Diagnostic(string code, DiagSeverity severity, string partName, string message)
        {
            Code = code;
            Severity = severity;
            PartName = partName;
            Message = message;
        }

        public override string ToString()
        {
            return "[" + Code + "] " + PartName + ": " + Message;
        }
    }

    // Тип твёрдого тела
    public enum SolidKind
    {
        Tessellated,
        Tube,
        Polycone,
        Box
    }

    // Геометрическая форма твёрдого тела
    public sealed class SolidShape
    {
        public SolidKind Kind { get; set; }
        public double RMin { get; set; }
        public double RMax { get; set; }
        public double Z0 { get; set; }
        public double Z1 { get; set; }
        public double X0 { get; set; }
        public double Y0 { get; set; }

        // Плоскости для поликонуса
        public List<double> ZPlane { get; set; }
        public List<double> RInner { get; set; }
        public List<double> ROuter { get; set; }

        // Габариты бокса
        public double BoxX { get; set; }
        public double BoxY { get; set; }
        public double BoxZ { get; set; }

        public SolidShape()
        {
            ZPlane = new List<double>();
            RInner = new List<double>();
            ROuter = new List<double>();
        }
    }

    // Деталь сцены
    public sealed class Part
    {
        public string Name { get; set; }
        public string OrigName { get; set; }
        public Mesh Mesh { get; set; }
        public PartRole Role { get; set; }
        public string Material { get; set; }
        public SolidShape Shape { get; set; }
        public double VolumeMm3 { get; set; }
        public string SourceFile { get; set; }
    }

    // Модель сцены: детали + диагностика
    public sealed class SceneModel
    {
        public List<Part> Parts { get; }
        public List<Diagnostic> Diagnostics { get; }
        public string Title { get; set; }
        public string SourceDescription { get; set; }

        public SceneModel()
        {
            Parts = new List<Part>();
            Diagnostics = new List<Diagnostic>();
        }

        // Добавить строку диагностики
        public void Add(string code, DiagSeverity severity, string part, string message)
        {
            Diagnostics.Add(new Diagnostic(code, severity, part, message));
        }
    }
}
