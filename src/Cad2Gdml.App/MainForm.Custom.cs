using System.Collections.Generic;
using System.Windows.Forms;
using Cad2Gdml.Materials;

namespace Cad2Gdml.App
{
    public sealed partial class MainForm : Form
    {
        // #CG-32: тот же список, что заполняет окно «Сцена и вещества»; уходит в PipelineOptions.CustomMaterials
        private List<MaterialDef> _customMaterials = SceneViewerForm.SessionCustom;
    }
}
