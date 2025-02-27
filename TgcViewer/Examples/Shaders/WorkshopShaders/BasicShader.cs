using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using TgcViewer.Example;
using TgcViewer;
using Microsoft.DirectX.Direct3D;
using Microsoft.DirectX;
using TgcViewer.Utils.TgcSceneLoader;
using System.Drawing;
using TgcViewer.Utils.TgcGeometry;
using TgcViewer.Utils.Terrain;
using TgcViewer.Utils.Input;
using TgcViewer.Utils.Shaders;

namespace Examples.Shaders.WorkshopShaders
{

    /// <summary>
    /// Ejemplo EnvMap:
    /// Unidades Involucradas:
    ///     # Unidad 8 - Adaptadores de Video - Shaders
    /// 
    /// Es el hola mundo de los shaders
    /// 
    /// Autor: Mariano Banquiero
    /// 
    /// </summary>
    public class BasicShader: TgcExample
    {
        Effect effect;
        TgcScene scene;
        TgcMesh mesh;
        float time;


        public override string getCategory()
        {
            return "Shaders";
        }

        public override string getName()
        {
            return "Workshop-BasicShader";
        }

        public override string getDescription()
        {
            return "Ejemplo de Shader Basico";
        }

        public override void init()
        {
            Device d3dDevice = GuiController.Instance.D3dDevice;
            //Crear loader
            TgcSceneLoader loader = new TgcSceneLoader();

            //Cargar los mesh:
            scene = loader.loadSceneFromFile(GuiController.Instance.ExamplesMediaDir
                            + "MeshCreator\\Meshes\\Vehiculos\\TanqueFuturistaRuedas\\TanqueFuturistaRuedas-TgcScene.xml");
            mesh = scene.Meshes[0];
            mesh.Scale = new Vector3(0.5f, 0.5f, 0.5f);
            mesh.Position = new Vector3(0f, 0f, 0f);

            //Cargar Shader personalizado
            effect = TgcShaders.loadEffect(GuiController.Instance.ExamplesDir + "Shaders\\WorkshopShaders\\Shaders\\BasicShader.fx");

            // le asigno el efecto a la malla 
            mesh.Effect = effect;

            // indico que tecnica voy a usar 
            // Hay effectos que estan organizados con mas de una tecnica.
            mesh.Technique = "RenderScene";

            //Centrar camara rotacional respecto a este mesh
            GuiController.Instance.RotCamera.targetObject(mesh.BoundingBox);

            time = 0;
        }


        public override void render(float elapsedTime)
        {
            Device device = GuiController.Instance.D3dDevice;
            time += elapsedTime;

            GuiController.Instance.RotCamera.targetObject(mesh.BoundingBox);
            GuiController.Instance.CurrentCamera.updateCamera();
            device.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.Black, 1.0f, 0);

            // Cargar variables de shader, por ejemplo el tiempo transcurrido.
            effect.SetValue("time", time);
            
            // dibujo la malla pp dicha
            mesh.render();
        }

        public override void close()
        {
            effect.Dispose();
            scene.disposeAll();
        }
    }

}
