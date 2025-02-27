using System;
using System.Collections.Generic;
using System.Text;
using TgcViewer.Utils.TgcSceneLoader;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using System.Drawing;
using System.Xml;
using System.IO;
using TgcViewer.Utils.TgcGeometry;

namespace TgcViewer.Utils.TgcSceneLoader
{
    /// <summary>
    /// Herramienta para exportar un conjunto de modelos TgcMesh a un archivo XML de formato TGC.
    /// Es similar a lo que hace el plugin de 3Ds MAX TgcSceneExporter.ms, pero hecho desde C#
    /// </summary>
    public class TgcSceneExporter
    {
        private const float EPSILON = 0.0001f;

        /// <summary>
        /// Resultado de exportacion
        /// </summary>
        public class ExportResult
        {
            public bool result;
            public bool secondaryErrors;
            public string filePath;
            public List<string> errors;

            public ExportResult()
            {
                result = true;
                secondaryErrors = false;
                errors = new List<string>();
            }

            public string listErrors()
            {
                StringBuilder sb = new StringBuilder(errors.Count + " errores: ");
                for (int i = 0; i < errors.Count; i++)
                {
                    sb.Append((i+1) + ") " + errors[i]);
                }
                return sb.ToString();
            }

        }

        public TgcSceneExporter()
        {
        }

        /// <summary>
        /// Graba una escena entera del tipo TgcScene a un archivo XML de tipo "-TgcScene.xml", que luego
        /// puede ser cargado con el TgcSceneLoader
        /// </summary>
        /// <param name="scene">Escena a exportar</param>
        /// <param name="saveFolderPath">Carpeta en la que se quiera guardar el XML</param>
        /// <returns>Resultado de exportacion</returns>
        public ExportResult exportSceneToXml(TgcScene scene, string saveFolderPath)
        {
            MeshExport[] meshesExport = exportSceneData(scene);
            return saveSceneToXml(scene.SceneName, createSceneBoundingBox(meshesExport), meshesExport, saveFolderPath);
        }

        /// <summary>
        /// Graba una escena entera del tipo TgcScene a un archivo XML de tipo "-TgcScene.xml", que luego
        /// puede ser cargado con el TgcSceneLoader
        /// Antes de generar el XML, unifica todas las mallas en una sola, adaptando sus coordendas de textura y Materials.
        /// Actualmente no se puede hacer con Mallas que tengan LightMaps.
        /// </summary>
        /// <param name="scene">Escena a exportar</param>
        /// <param name="saveFolderPath">Carpeta en la que se quiera guardar el XML</param>
        /// <returns>Resultado de exportacion</returns>
        public ExportResult exportAndAppendSceneToXml(TgcScene scene, string saveFolderPath)
        {
            MeshExport meshExportFinal = exportAndAppendSceneData(scene);
            MeshExport[] meshesExport = new MeshExport[] { meshExportFinal };
            return saveSceneToXml(scene.SceneName, createSceneBoundingBox(meshesExport), meshesExport, saveFolderPath);
        }

        /// <summary>
        /// Crear BoundingBox para escena
        /// </summary>
        private TgcBoundingBox createSceneBoundingBox(MeshExport[] meshesExport)
        {
            List<TgcBoundingBox> boundingBoxes = new List<TgcBoundingBox>();
            foreach (MeshExport m in meshesExport)
            {
                boundingBoxes.Add(new TgcBoundingBox(
                    TgcParserUtils.float3ArrayToVector3(m.MeshData.pMin),
                    TgcParserUtils.float3ArrayToVector3(m.MeshData.pMax)));
            }
            return TgcBoundingBox.computeFromBoundingBoxes(boundingBoxes);
        }



        #region Export MeshData


        /// <summary>
        /// Exporta los datos de todas los TgcMesh de una escena. Los exporta a un formato de objetos plano
        /// </summary>
        /// <param name="scene">Escena a exportar</param>
        /// <returns>Datos de la mallas en objetos</returns>
        public MeshExport[] exportSceneData(TgcScene scene)
        {
            MeshExport[] meshesExport = new MeshExport[scene.Meshes.Count];
            for (int i = 0; i < scene.Meshes.Count; i++)
            {
                meshesExport[i] = exportMeshData(scene.Meshes[i], scene.Meshes);
            }
            return meshesExport;
        }

        /// <summary>
        /// Exporta los datos de todas los TgcMesh de una escena. Los exporta a un formato de objetos plano.
        /// Unifica todas las Mallas de la escena en una sola, adaptando sus coordendas de textura y Materials.
        /// Actualmente no se puede hacer con Mallas que tengan LightMaps.
        /// </summary>
        /// <param name="scene">Escena a unificar y exportar</param>
        /// <returns>Datos de la escena unificada y exportada</returns>
        public MeshExport exportAndAppendSceneData(TgcScene scene)
        {
            MeshExport[] meshesExport = exportSceneData(scene);
            return appendAllMeshes(scene.SceneName, meshesExport);
        }

        /// <summary>
        /// Toma los datos de un TgcMesh y los exporta a un formato de objetos plano
        /// </summary>
        /// <param name="tgcMesh">Malla a exportar</param>
        /// <returns>Datos de la malla en objetos</returns>
        public MeshExport exportMeshData(TgcMesh tgcMesh, List<TgcMesh> sceneMeshes)
        {
            try
            {
                MeshExport meshExport = new MeshExport();
                TgcMeshData meshData = new TgcMeshData();
                meshExport.MeshData = meshData;
                meshExport.MaterialsData = null;
                meshExport.diffuseMapsAbsolutePath = null;

                //General
                meshExport.MeshData.name = tgcMesh.Name;
                meshExport.MeshData.layerName = tgcMesh.Layer;
                meshExport.MeshRenderType = tgcMesh.RenderType;
                meshExport.MeshData.pMin = TgcParserUtils.vector3ToFloat3Array(tgcMesh.BoundingBox.PMin);
                meshExport.MeshData.pMax = TgcParserUtils.vector3ToFloat3Array(tgcMesh.BoundingBox.PMax);
                meshExport.MeshData.userProperties = tgcMesh.UserProperties;
                meshExport.MeshData.alphaBlending = tgcMesh.AlphaBlendEnable;

                //Exportar malla original
                if (tgcMesh.ParentInstance == null)
                {
                    meshExport.MeshData.instanceType = TgcMeshData.ORIGINAL;

                    //Exportar segun el tipo de Mesh
                    switch (tgcMesh.RenderType)
                    {
                        case TgcMesh.MeshRenderType.VERTEX_COLOR:
                            exportMeshVertexColor(tgcMesh, meshExport, meshData);
                            break;

                        case TgcMesh.MeshRenderType.DIFFUSE_MAP:
                            exportMeshDiffuseMap(tgcMesh, meshExport, meshData);
                            break;

                        case TgcMesh.MeshRenderType.DIFFUSE_MAP_AND_LIGHTMAP:
                            exportMeshDiffuseMapAndLightmap(tgcMesh, meshExport, meshData);
                            break;
                    }
                }

                //Exportar malla instancia
                else
                {
                    meshExport.MeshData.instanceType = TgcMeshData.INSTANCE;

                    //Buscar indice de original
                    TgcMesh parentInstance = tgcMesh.ParentInstance;
                    int parentIdx = -1;
                    for (int i = 0; i < sceneMeshes.Count; i++)
	                {
                		 if(parentInstance.Equals(sceneMeshes[i]))
                         {
                             parentIdx = i;
                             break;
                         }
	                }
                    meshExport.MeshData.originalMesh = parentIdx;

                    //TODO: la rotaci�n no se exporta correctamente cuando la malla original esta rotada

                    //Posicion, rotacion y escala con diferencia de la malla original
                    meshExport.MeshData.position = TgcParserUtils.vector3ToFloat3Array(tgcMesh.Position - parentInstance.Position);
                    Quaternion rotQuat = Quaternion.RotationYawPitchRoll(tgcMesh.Rotation.Y, tgcMesh.Rotation.X, tgcMesh.Rotation.Z);
                    Quaternion parentRotQuat = Quaternion.RotationYawPitchRoll(parentInstance.Rotation.Y, parentInstance.Rotation.X, parentInstance.Rotation.Z);
                    meshExport.MeshData.rotation = TgcParserUtils.quaternionToFloat4Array(rotQuat - parentRotQuat);
                    Vector3 scale = new Vector3(
                        tgcMesh.Scale.X / parentInstance.Scale.X,
                        tgcMesh.Scale.Y / parentInstance.Scale.Y,
                        tgcMesh.Scale.Z / parentInstance.Scale.Z
                        );
                    meshExport.MeshData.scale = TgcParserUtils.vector3ToFloat3Array(scale);
                }

                



                return meshExport;
            }
            catch (Exception ex)
            {
                throw new Exception("Error al intentar obtener datos de Mesh para exportar. MeshName: " + tgcMesh.Name, ex);
            }
            
        }

        
        /// <summary>
        /// Exportar datos de Mesh de tipo TgcMesh.MeshRenderType.VERTEX_COLOR
        /// </summary>
        private void exportMeshVertexColor(TgcMesh tgcMesh, MeshExport meshExport, TgcMeshData meshData)
        {
            //Marcar todo lo que no tiene
            meshData.lightmap = null;
            meshData.texCoordinatesIndices = new int[0];
            meshData.textureCoordinates = new float[0];
            meshData.texCoordinatesIndicesLightMap = new int[0];
            meshData.textureCoordinatesLightMap = new float[0];
            meshExport.MaterialsData = null;
            meshExport.diffuseMapsAbsolutePath = null;
            meshExport.lightmapAbsolutePath = null;
            meshData.materialId = -1;
            meshData.materialsIds = new int[0];

            //Color general
            Color defaultColor = Color.White;
            ColorValue defaultColorValue = ColorValue.FromColor(defaultColor);
            meshData.color = new float[] { defaultColorValue.Red, defaultColorValue.Green, defaultColorValue.Blue };

            //Obtener datos del VertexBuffer
            TgcSceneLoader.VertexColorVertex[] vbData = (TgcSceneLoader.VertexColorVertex[])tgcMesh.D3dMesh.LockVertexBuffer(
                typeof(TgcSceneLoader.VertexColorVertex),
                LockFlags.ReadOnly,
                tgcMesh.D3dMesh.NumberVertices);
            tgcMesh.D3dMesh.UnlockVertexBuffer();

            short[] indices = (short[])tgcMesh.D3dMesh.LockIndexBuffer(typeof(short), LockFlags.ReadOnly, tgcMesh.D3dMesh.NumberFaces * 3);
            tgcMesh.D3dMesh.UnlockIndexBuffer();

            //Armar buffer de vertices, normales y coordenadas de textura, buscando similitudes de valores
            List<int> coordinatesIndices = new List<int>();
            List<int> colorIndices = new List<int>();
            List<Vector3> verticesCoordinates = new List<Vector3>();
            List<int> verticesColors = new List<int>();
            List<Vector3> verticesNormals = new List<Vector3>();
            for (int i = 0; i < indices.Length; i++)
            {
                TgcSceneLoader.VertexColorVertex vertexData = vbData[indices[i]];
                Vector3 position = Vector3.TransformCoordinate(vertexData.Position, tgcMesh.Transform);

                int coordIdx = addVertex(coordinatesIndices, verticesCoordinates, position);
                addNormal(verticesNormals, coordIdx, vertexData.Normal);
                addColor(colorIndices, verticesColors, vertexData.Color);
                
            }

            //Cargar array de vertices
            meshData.coordinatesIndices = coordinatesIndices.ToArray();
            meshData.verticesCoordinates = new float[verticesCoordinates.Count * 3];
            for (int i = 0; i < verticesCoordinates.Count; i++)
            {
                Vector3 v = verticesCoordinates[i];
                meshData.verticesCoordinates[i * 3] = v.X;
                meshData.verticesCoordinates[i * 3 + 1] = v.Y;
                meshData.verticesCoordinates[i * 3 + 2] = v.Z;
            }

            //Cargar array de normales
            meshData.verticesNormals = new float[verticesNormals.Count * 3];
            for (int i = 0; i < verticesNormals.Count; i++)
            {
                Vector3 n = verticesNormals[i];
                meshData.verticesNormals[i * 3] = n.X;
                meshData.verticesNormals[i * 3 + 1] = n.Y;
                meshData.verticesNormals[i * 3 + 2] = n.Z;
            }

            //Cargar array de colores
            meshData.colorIndices = colorIndices.ToArray();
            meshData.verticesColors = new int[verticesColors.Count * 3];
            for (int i = 0; i < verticesColors.Count; i++)
            {
                Color c = Color.FromArgb(verticesColors[i]);
                meshData.verticesColors[i * 3] = c.R;
                meshData.verticesColors[i * 3 + 1] = c.G;
                meshData.verticesColors[i * 3 + 2] = c.B;
            }
            

        }

        

        /// <summary>
        /// Exportar datos de Malla del tipo TgcMesh.MeshRenderType.DIFFUSE_MAP
        /// </summary>
        private void exportMeshDiffuseMap(TgcMesh tgcMesh, MeshExport meshExport, TgcMeshData meshData)
        {
            //Marcar como null todo lo que no tiene
            meshData.lightmap = null;
            meshData.texCoordinatesIndicesLightMap = new int[0];
            meshData.textureCoordinatesLightMap = new float[0];

            //Color general
            Color defaultColor = Color.White;
            ColorValue defaultColorValue = ColorValue.FromColor(defaultColor);
            meshData.color = new float[] { defaultColorValue.Red, defaultColorValue.Green, defaultColorValue.Blue };

            //Obtener datos del VertexBuffer
            TgcSceneLoader.DiffuseMapVertex[] vbData = (TgcSceneLoader.DiffuseMapVertex[])tgcMesh.D3dMesh.LockVertexBuffer(
                typeof(TgcSceneLoader.DiffuseMapVertex),
                LockFlags.ReadOnly,
                tgcMesh.D3dMesh.NumberVertices);
            tgcMesh.D3dMesh.UnlockVertexBuffer();

            short[] indices = (short[])tgcMesh.D3dMesh.LockIndexBuffer(typeof(short), LockFlags.ReadOnly, tgcMesh.D3dMesh.NumberFaces*3);
            tgcMesh.D3dMesh.UnlockIndexBuffer();

            //Armar buffer de vertices, normales y coordenadas de textura, buscando similitudes de valores
            List<int> coordinatesIndices = new List<int>();
            List<int> texCoordinatesIndices = new List<int>();
            List<Vector3> verticesCoordinates = new List<Vector3>();
            List<Vector2> textureCoordinates = new List<Vector2>();
            List<Vector3> verticesNormals = new List<Vector3>();
            List<int> colorIndices = new List<int>();
            List<int> verticesColors = new List<int>();
            

            for (int i = 0; i < indices.Length; i++)            
            {
                TgcSceneLoader.DiffuseMapVertex vertexData = vbData[indices[i]];
                Vector3 position = Vector3.TransformCoordinate(vertexData.Position, tgcMesh.Transform);

                int coordIdx = addVertex(coordinatesIndices, verticesCoordinates, position);
                addNormal(verticesNormals, coordIdx, vertexData.Normal);
                addTextureCoordinates(texCoordinatesIndices, textureCoordinates, new Vector2(vertexData.Tu, vertexData.Tv));
                addColor(colorIndices, verticesColors, vertexData.Color);
            }

            //Cargar array de vertices
            meshData.coordinatesIndices = coordinatesIndices.ToArray();
            meshData.verticesCoordinates = new float[verticesCoordinates.Count * 3];
            for (int i = 0; i < verticesCoordinates.Count; i++)
            {
                Vector3 v = verticesCoordinates[i];
                meshData.verticesCoordinates[i * 3] = v.X;
                meshData.verticesCoordinates[i * 3 + 1] = v.Y;
                meshData.verticesCoordinates[i * 3 + 2] = v.Z;
            }

            //Cargar array de normales
            meshData.verticesNormals = new float[verticesNormals.Count * 3];
            for (int i = 0; i < verticesNormals.Count; i++)
            {
                Vector3 n = verticesNormals[i];
                meshData.verticesNormals[i * 3] = n.X;
                meshData.verticesNormals[i * 3 + 1] = n.Y;
                meshData.verticesNormals[i * 3 + 2] = n.Z;
            }

            //Cargar array de coordenadas de textura
            meshData.texCoordinatesIndices = texCoordinatesIndices.ToArray();
            meshData.textureCoordinates = new float[textureCoordinates.Count * 2];
            for (int i = 0; i < textureCoordinates.Count; i++)
            {
                Vector2 t = textureCoordinates[i];
                meshData.textureCoordinates[i * 2] = t.X;
                meshData.textureCoordinates[i * 2 + 1] = t.Y;
            }

            //Cargar array de colores
            meshData.colorIndices = colorIndices.ToArray();
            meshData.verticesColors = new int[verticesColors.Count * 3];
            for (int i = 0; i < verticesColors.Count; i++)
            {
                Color c = Color.FromArgb(verticesColors[i]);
                meshData.verticesColors[i * 3] = c.R;
                meshData.verticesColors[i * 3 + 1] = c.G;
                meshData.verticesColors[i * 3 + 2] = c.B;
            }

            //Exportar Materials y DiffuseMaps
            exportMaterialData(tgcMesh, meshExport, meshData);
        }


        /// <summary>
        /// Exportar datos de Mesh de formato TgcMesh.MeshRenderType.DIFFUSE_MAP_AND_LIGHTMAP
        /// </summary>
        private void exportMeshDiffuseMapAndLightmap(TgcMesh tgcMesh, MeshExport meshExport, TgcMeshData meshData)
        {
            //Obtener datos del VertexBuffer
            TgcSceneLoader.DiffuseMapAndLightmapVertex[] vbData = (TgcSceneLoader.DiffuseMapAndLightmapVertex[])tgcMesh.D3dMesh.LockVertexBuffer(
                typeof(TgcSceneLoader.DiffuseMapAndLightmapVertex),
                LockFlags.ReadOnly,
                tgcMesh.D3dMesh.NumberVertices);
            tgcMesh.D3dMesh.UnlockVertexBuffer();

            short[] indices = (short[])tgcMesh.D3dMesh.LockIndexBuffer(typeof(short), LockFlags.ReadOnly, tgcMesh.D3dMesh.NumberFaces * 3);
            tgcMesh.D3dMesh.UnlockIndexBuffer();

            //Color general
            Color defaultColor = Color.White;
            ColorValue defaultColorValue = ColorValue.FromColor(defaultColor);
            meshData.color = new float[] { defaultColorValue.Red, defaultColorValue.Green, defaultColorValue.Blue };

            //Armar buffer de vertices, normales y coordenadas de textura, buscando similitudes de valores
            List<int> coordinatesIndices = new List<int>();
            List<int> texCoordinatesIndices = new List<int>();
            List<int> texCoordinatesIndicesLightMap = new List<int>();
            List<Vector3> verticesCoordinates = new List<Vector3>();
            List<Vector2> textureCoordinates = new List<Vector2>();
            List<Vector2> textureCoordinatesLightMap = new List<Vector2>();
            List<Vector3> verticesNormals = new List<Vector3>();
            List<int> colorIndices = new List<int>();
            List<int> verticesColors = new List<int>();
            

            for (int i = 0; i < indices.Length; i++)
            {
                TgcSceneLoader.DiffuseMapAndLightmapVertex vertexData = vbData[indices[i]];
                Vector3 position = Vector3.TransformCoordinate(vertexData.Position, tgcMesh.Transform);

                int coordIdx = addVertex(coordinatesIndices, verticesCoordinates, position);
                addNormal(verticesNormals, coordIdx, vertexData.Normal);
                addTextureCoordinates(texCoordinatesIndices, textureCoordinates, new Vector2(vertexData.Tu0, vertexData.Tv0));
                addTextureCoordinates(texCoordinatesIndicesLightMap, textureCoordinatesLightMap, new Vector2(vertexData.Tu1, vertexData.Tv1));
                addColor(colorIndices, verticesColors, vertexData.Color);
            }

            //Cargar array de vertices
            meshData.coordinatesIndices = coordinatesIndices.ToArray();
            meshData.verticesCoordinates = new float[verticesCoordinates.Count * 3];
            for (int i = 0; i < verticesCoordinates.Count; i++)
            {
                Vector3 v = verticesCoordinates[i];
                meshData.verticesCoordinates[i * 3] = v.X;
                meshData.verticesCoordinates[i * 3 + 1] = v.Y;
                meshData.verticesCoordinates[i * 3 + 2] = v.Z;
            }

            //Cargar array de normales
            meshData.verticesNormals = new float[verticesNormals.Count * 3];
            for (int i = 0; i < verticesNormals.Count; i++)
            {
                Vector3 n = verticesNormals[i];
                meshData.verticesNormals[i * 3] = n.X;
                meshData.verticesNormals[i * 3 + 1] = n.Y;
                meshData.verticesNormals[i * 3 + 2] = n.Z;
            }

            //Cargar array de coordenadas de textura
            meshData.texCoordinatesIndices = texCoordinatesIndices.ToArray();
            meshData.textureCoordinates = new float[textureCoordinates.Count * 2];
            for (int i = 0; i < textureCoordinates.Count; i++)
            {
                Vector2 t = textureCoordinates[i];
                meshData.textureCoordinates[i * 2] = t.X;
                meshData.textureCoordinates[i * 2 + 1] = t.Y;
            }

            //Cargar array de coordenadas de textura de Lightmap
            meshData.texCoordinatesIndicesLightMap = texCoordinatesIndicesLightMap.ToArray();
            meshData.textureCoordinatesLightMap = new float[textureCoordinatesLightMap.Count * 2];
            for (int i = 0; i < textureCoordinatesLightMap.Count; i++)
            {
                Vector2 t = textureCoordinatesLightMap[i];
                meshData.textureCoordinatesLightMap[i * 2] = t.X;
                meshData.textureCoordinatesLightMap[i * 2 + 1] = t.Y;
            }

            //Cargar array de colores
            meshData.colorIndices = colorIndices.ToArray();
            meshData.verticesColors = new int[verticesColors.Count * 3];
            for (int i = 0; i < verticesColors.Count; i++)
            {
                Color c = Color.FromArgb(verticesColors[i]);
                meshData.verticesColors[i * 3] = c.R;
                meshData.verticesColors[i * 3 + 1] = c.G;
                meshData.verticesColors[i * 3 + 2] = c.B;
            }

            //Exportar Materials y DiffuseMaps
            exportMaterialData(tgcMesh, meshExport, meshData);


            //Exportar Lightmap
            TgcTexture tgcLightmap = tgcMesh.LightMap;
            meshData.lightmap = tgcLightmap.FileName;
            meshExport.lightmapAbsolutePath = tgcLightmap.FilePath;
        }




        /// <summary>
        /// Exportar datos de Material
        /// </summary>
        private void exportMaterialData(TgcMesh tgcMesh, MeshExport meshExport, TgcMeshData meshData)
        {
            //Exportar diffuseMap y material simple
            bool alphaBlendEnabled = false;
            if (tgcMesh.Materials.Length == 1)
            {
                TgcMaterialData materialData = new TgcMaterialData();
                meshExport.MaterialsData = new TgcMaterialData[] { materialData };

                //Material
                Material tgcMaterial = tgcMesh.Materials[0];
                materialData.type = TgcMaterialData.StandardMaterial;
                materialData.name = TgcMaterialData.StandardMaterial;
                materialData.subMaterials = null;

                materialData.ambientColor = new float[]{
                            tgcMaterial.AmbientColor.Red,
                            tgcMaterial.AmbientColor.Green,
                            tgcMaterial.AmbientColor.Blue,
                            tgcMaterial.AmbientColor.Alpha,
                        };
                materialData.diffuseColor = new float[]{
                            tgcMaterial.DiffuseColor.Red,
                            tgcMaterial.DiffuseColor.Green,
                            tgcMaterial.DiffuseColor.Blue,
                            tgcMaterial.DiffuseColor.Alpha,
                        };
                materialData.specularColor = new float[]{
                            tgcMaterial.SpecularColor.Red,
                            tgcMaterial.SpecularColor.Green,
                            tgcMaterial.SpecularColor.Blue,
                            tgcMaterial.SpecularColor.Alpha,
                        };
                materialData.opacity = 1f;
                materialData.alphaBlendEnable = tgcMesh.AlphaBlendEnable;


                //Texture
                TgcTexture tgcTexture = tgcMesh.DiffuseMaps[0];
                materialData.fileName = tgcTexture.FileName;
                materialData.uvOffset = new float[] { 1f, 1f };
                materialData.uvTiling = new float[] { 1f, 1f };
                meshExport.diffuseMapsAbsolutePath = new string[] { tgcTexture.FilePath };

                //Configurar mesh
                meshData.materialId = 0;
                meshData.materialsIds = new int[] { meshData.materialId };

            }
            //Exportar varios diffuseMaps y materials 
            else
            {
                meshExport.MaterialsData = new TgcMaterialData[tgcMesh.Materials.Length];
                meshExport.diffuseMapsAbsolutePath = new string[tgcMesh.Materials.Length];
                for (int i = 0; i < tgcMesh.Materials.Length; i++)
                {
                    //Material
                    Material tgcMaterial = tgcMesh.Materials[i];
                    TgcMaterialData materialData = new TgcMaterialData();
                    meshExport.MaterialsData[i] = materialData;
                    materialData.type = TgcMaterialData.StandardMaterial;
                    materialData.name = TgcMaterialData.StandardMaterial;
                    materialData.subMaterials = null;

                    materialData.ambientColor = new float[]{
                                tgcMaterial.AmbientColor.Red,
                                tgcMaterial.AmbientColor.Green,
                                tgcMaterial.AmbientColor.Blue,
                                tgcMaterial.AmbientColor.Alpha,
                            };
                    materialData.diffuseColor = new float[]{
                                tgcMaterial.DiffuseColor.Red,
                                tgcMaterial.DiffuseColor.Green,
                                tgcMaterial.DiffuseColor.Blue,
                                tgcMaterial.DiffuseColor.Alpha,
                            };
                    materialData.specularColor = new float[]{
                                tgcMaterial.SpecularColor.Red,
                                tgcMaterial.SpecularColor.Green,
                                tgcMaterial.SpecularColor.Blue,
                                tgcMaterial.SpecularColor.Alpha,
                            };
                    materialData.opacity = 1f;
                    materialData.alphaBlendEnable = tgcMesh.AlphaBlendEnable;

                    //Texture
                    TgcTexture tgcTexture = tgcMesh.DiffuseMaps[i];
                    materialData.fileName = tgcTexture.FileName;
                    materialData.uvOffset = new float[] { 1f, 1f };
                    materialData.uvTiling = new float[] { 1f, 1f };
                    meshExport.diffuseMapsAbsolutePath[i] = tgcTexture.FilePath;
                }

                //Configurar Mesh
                meshData.materialId = 0;
                meshData.materialsIds = tgcMesh.D3dMesh.LockAttributeBufferArray(LockFlags.ReadOnly);
                tgcMesh.D3dMesh.UnlockAttributeBuffer();
            }
        }

        /// <summary>
        /// Agregar un vertice sin repetir
        /// </summary>
        private int addVertex(List<int> coordinatesIndices, List<Vector3> verticesCoordinates, Vector3 vertex)
        {
            for (int i = 0; i < verticesCoordinates.Count; i++)
            {
                Vector3 v = verticesCoordinates[i];
                if (equalsVector3(v, vertex))
                {
                    coordinatesIndices.Add(i);
                    return i;
                }
            }

            verticesCoordinates.Add(vertex);
            int newIdx = verticesCoordinates.Count - 1;
            coordinatesIndices.Add(newIdx);
            return newIdx;
        }

        /// <summary>
        /// Agregar una coordenada de textura sin repetir
        /// </summary>
        private int addTextureCoordinates(List<int> texCoordinatesIndices, List<Vector2> textureCoordinates, Vector2 texCoord)
        {
            for (int i = 0; i < textureCoordinates.Count; i++)
            {
                Vector2 t = textureCoordinates[i];
                if (equalsVector2(t, texCoord))
                {
                    texCoordinatesIndices.Add(i);
                    return i;
                }
            }

            textureCoordinates.Add(texCoord);
            int newIdx = textureCoordinates.Count - 1;
            texCoordinatesIndices.Add(newIdx);
            return newIdx;
        }

        /// <summary>
        /// Agregar una normal en base a los indices de vertices
        /// </summary>
        private void addNormal(List<Vector3> verticesNormals, int coordIdx, Vector3 normal)
        {
            if (coordIdx == verticesNormals.Count)
            {
                verticesNormals.Add(normal);
            }
        }

        /// <summary>
        /// Agregar un color sin repetir
        /// </summary>
        private int addColor(List<int> colorIndices, List<int> verticesColors, int color)
        {
            for (int i = 0; i < verticesColors.Count; i++)
            {
                int c = verticesColors[i];
                if (c == color)
                {
                    colorIndices.Add(i);
                    return i;
                }
            }

            verticesColors.Add(color);
            int newIdx = verticesColors.Count - 1;
            colorIndices.Add(newIdx);
            return newIdx;
        }


        /// <summary>
        /// Compara que dos Vector3 sean iguales, o casi
        /// </summary>
        private bool equalsVector3(Vector3 v1, Vector3 v2)
        {
            return equalsFloat(v1.X, v2.X) && equalsFloat(v1.Y, v2.Y) && equalsFloat(v1.Z, v2.Z);
        }

        /// <summary>
        /// Compara que dos Vector2 sean iguales, o casi
        /// </summary>
        private bool equalsVector2(Vector2 v1, Vector2 v2)
        {
            return equalsFloat(v1.X, v2.X) && equalsFloat(v1.Y, v2.Y);
        }

        /// <summary>
        /// Compara que dos floats sean iguales, o casi
        /// </summary>
        private bool equalsFloat(float f1, float f2)
        {
            return Math.Abs(f1 - f2) <= EPSILON;
        }

        

       

        /// <summary>
        /// Datos de un TgcMesh exportado
        /// </summary>
        public class MeshExport
        {
            public TgcMeshData MeshData;
            public TgcMaterialData[] MaterialsData;
            public string[] diffuseMapsAbsolutePath;
            public string lightmapAbsolutePath;
            public TgcMesh.MeshRenderType MeshRenderType;
        }


        #endregion


        #region Append Meshes

        /// <summary>
        /// Unifica N Mallas en una sola, adaptando sus coordendas de textura y Materials.
        /// Actualmente no se puede hacer con Mallas que tengan LightMaps.
        /// </summary>
        /// <param name="meshName">Nuevo nombre de malla</param>
        /// <param name="meshesExport">Array de mallas a unificar</param>
        /// <returns>Datos de nueva malla unificada</returns>
        public MeshExport appendAllMeshes(string meshName, MeshExport[] meshesExport)
        {
            MeshExport finalMesh = meshesExport[0];
            for (int i = 1; i < meshesExport.Length; i++)
            {
                finalMesh = appendMeshes(meshName, finalMesh, meshesExport[i]);
            }
            return finalMesh;
        }

        /// <summary>
        /// Unifica dos Mallas en una, adaptando sus coordendas de textura y Materials.
        /// Actualmente no se puede hacer con Mallas que tengan LightMaps.
        /// </summary>
        /// <param name="meshName">Nuevo nombre de malla</param>
        /// <param name="mExp1">Datos de malla 1</param>
        /// <param name="mExp2">Datos de malla 2</param>
        /// <returns>Datos de nueva malla unificada</returns>
        public MeshExport appendMeshes(string meshName, MeshExport mExp1, MeshExport mExp2)
        {
            //Chequear que sea mismo tipo de malla
            if (mExp1.MeshRenderType != mExp2.MeshRenderType)
            {
                throw new Exception("Se intent� juntar dos Mallas de formato distintos: " + mExp1.MeshData.name + " y " + mExp2.MeshData.name);
            }

            //Por ahora no se pueden unificar LightMaps
            if (mExp1.MeshRenderType == TgcMesh.MeshRenderType.DIFFUSE_MAP_AND_LIGHTMAP)
            {
                throw new Exception("Actualmente no esta soportado juntar dos Mallas que tienen LightMaps: " + mExp1.MeshData.name + " y " + mExp2.MeshData.name);
            }


            //General
            MeshExport meshExpAppended = new MeshExport();
            meshExpAppended.MeshData = new TgcMeshData();
            meshExpAppended.MeshData.name = meshName;
            meshExpAppended.MeshData.layerName = mExp1.MeshData.layerName;
            meshExpAppended.lightmapAbsolutePath = null;
            meshExpAppended.MeshData.lightmap = null;
            meshExpAppended.MeshData.color = mExp1.MeshData.color;
            meshExpAppended.MeshData.alphaBlending = mExp1.MeshData.alphaBlending | mExp2.MeshData.alphaBlending;
            meshExpAppended.MeshData.instanceType = TgcMeshData.ORIGINAL;

            //BoundingBox que une ambas
            List<TgcBoundingBox> bboxes = new List<TgcBoundingBox>();
            bboxes.Add(new TgcBoundingBox(TgcParserUtils.float3ArrayToVector3(mExp1.MeshData.pMin), TgcParserUtils.float3ArrayToVector3(mExp1.MeshData.pMax)));
            bboxes.Add(new TgcBoundingBox(TgcParserUtils.float3ArrayToVector3(mExp2.MeshData.pMin), TgcParserUtils.float3ArrayToVector3(mExp2.MeshData.pMax)));
            TgcBoundingBox appendenBbox = TgcBoundingBox.computeFromBoundingBoxes(bboxes);
            meshExpAppended.MeshData.pMin = TgcParserUtils.vector3ToFloat3Array(appendenBbox.PMin);
            meshExpAppended.MeshData.pMax = TgcParserUtils.vector3ToFloat3Array(appendenBbox.PMax);


            //coordinatesIndices
            meshExpAppended.MeshData.coordinatesIndices = new int[mExp1.MeshData.coordinatesIndices.Length + mExp2.MeshData.coordinatesIndices.Length];
            Array.Copy(mExp1.MeshData.coordinatesIndices, 0, meshExpAppended.MeshData.coordinatesIndices, 0, mExp1.MeshData.coordinatesIndices.Length);
            Array.Copy(mExp2.MeshData.coordinatesIndices, 0, meshExpAppended.MeshData.coordinatesIndices, mExp1.MeshData.coordinatesIndices.Length, mExp2.MeshData.coordinatesIndices.Length);

            //verticesCoordinates
            meshExpAppended.MeshData.verticesCoordinates = new float[mExp1.MeshData.verticesCoordinates.Length + mExp2.MeshData.verticesCoordinates.Length];
            Array.Copy(mExp1.MeshData.verticesCoordinates, 0, meshExpAppended.MeshData.verticesCoordinates, 0, mExp1.MeshData.verticesCoordinates.Length);
            Array.Copy(mExp2.MeshData.verticesCoordinates, 0, meshExpAppended.MeshData.verticesCoordinates, mExp1.MeshData.verticesCoordinates.Length, mExp2.MeshData.verticesCoordinates.Length);
            
            //Ajustar indices de coordinatesIndices del segundo mesh
            for (int i = mExp1.MeshData.coordinatesIndices.Length; i < meshExpAppended.MeshData.coordinatesIndices.Length; i++)
            {
                meshExpAppended.MeshData.coordinatesIndices[i] += mExp1.MeshData.verticesCoordinates.Length / 3;
            }

            //texCoordinatesIndices
            meshExpAppended.MeshData.texCoordinatesIndices = new int[mExp1.MeshData.texCoordinatesIndices.Length + mExp2.MeshData.texCoordinatesIndices.Length];
            Array.Copy(mExp1.MeshData.texCoordinatesIndices, 0, meshExpAppended.MeshData.texCoordinatesIndices, 0, mExp1.MeshData.texCoordinatesIndices.Length);
            Array.Copy(mExp2.MeshData.texCoordinatesIndices, 0, meshExpAppended.MeshData.texCoordinatesIndices, mExp1.MeshData.texCoordinatesIndices.Length, mExp2.MeshData.texCoordinatesIndices.Length);
            
            //textureCoordinates
            meshExpAppended.MeshData.textureCoordinates = new float[mExp1.MeshData.textureCoordinates.Length + mExp2.MeshData.textureCoordinates.Length];
            Array.Copy(mExp1.MeshData.textureCoordinates, 0, meshExpAppended.MeshData.textureCoordinates, 0, mExp1.MeshData.textureCoordinates.Length);
            Array.Copy(mExp2.MeshData.textureCoordinates, 0, meshExpAppended.MeshData.textureCoordinates, mExp1.MeshData.textureCoordinates.Length, mExp2.MeshData.textureCoordinates.Length);

            //Ajustar indices de textureCoordinates del segundo mesh
            for (int i = mExp1.MeshData.texCoordinatesIndices.Length; i < meshExpAppended.MeshData.texCoordinatesIndices.Length; i++)
            {
                meshExpAppended.MeshData.texCoordinatesIndices[i] += mExp1.MeshData.textureCoordinates.Length / 2 ;
            }

            //colorIndices
            meshExpAppended.MeshData.colorIndices = new int[mExp1.MeshData.colorIndices.Length + mExp2.MeshData.colorIndices.Length];
            Array.Copy(mExp1.MeshData.colorIndices, 0, meshExpAppended.MeshData.colorIndices, 0, mExp1.MeshData.colorIndices.Length);
            Array.Copy(mExp2.MeshData.colorIndices, 0, meshExpAppended.MeshData.colorIndices, mExp1.MeshData.colorIndices.Length, mExp2.MeshData.colorIndices.Length);
            
            //verticesColors
            meshExpAppended.MeshData.verticesColors = new int[mExp1.MeshData.verticesColors.Length + mExp2.MeshData.verticesColors.Length];
            Array.Copy(mExp1.MeshData.verticesColors, 0, meshExpAppended.MeshData.verticesColors, 0, mExp1.MeshData.verticesColors.Length);
            Array.Copy(mExp2.MeshData.verticesColors, 0, meshExpAppended.MeshData.verticesColors, mExp1.MeshData.verticesColors.Length, mExp2.MeshData.verticesColors.Length);

            //Ajustar indices de verticesColors del segundo mesh
            for (int i = mExp1.MeshData.colorIndices.Length; i < meshExpAppended.MeshData.colorIndices.Length; i++)
            {
                meshExpAppended.MeshData.colorIndices[i] += mExp1.MeshData.verticesColors.Length / 3;
            }

            //texCoordinatesIndicesLightMap
            meshExpAppended.MeshData.texCoordinatesIndicesLightMap = new int[mExp1.MeshData.texCoordinatesIndicesLightMap.Length + mExp2.MeshData.texCoordinatesIndicesLightMap.Length];
            Array.Copy(mExp1.MeshData.texCoordinatesIndicesLightMap, 0, meshExpAppended.MeshData.texCoordinatesIndicesLightMap, 0, mExp1.MeshData.texCoordinatesIndicesLightMap.Length);
            Array.Copy(mExp2.MeshData.texCoordinatesIndicesLightMap, 0, meshExpAppended.MeshData.texCoordinatesIndicesLightMap, mExp1.MeshData.texCoordinatesIndicesLightMap.Length, mExp2.MeshData.texCoordinatesIndicesLightMap.Length);

            //textureCoordinatesLightMap
            meshExpAppended.MeshData.textureCoordinatesLightMap = new float[mExp1.MeshData.textureCoordinatesLightMap.Length + mExp2.MeshData.textureCoordinatesLightMap.Length];
            Array.Copy(mExp1.MeshData.textureCoordinatesLightMap, 0, meshExpAppended.MeshData.textureCoordinatesLightMap, 0, mExp1.MeshData.textureCoordinatesLightMap.Length);
            Array.Copy(mExp2.MeshData.textureCoordinatesLightMap, 0, meshExpAppended.MeshData.textureCoordinatesLightMap, mExp1.MeshData.textureCoordinatesLightMap.Length, mExp2.MeshData.textureCoordinatesLightMap.Length);

            //Ajustar indices de textureCoordinatesLightMap del segundo mesh
            for (int i = mExp1.MeshData.texCoordinatesIndicesLightMap.Length; i < meshExpAppended.MeshData.texCoordinatesIndicesLightMap.Length; i++)
            {
                meshExpAppended.MeshData.texCoordinatesIndicesLightMap[i] += mExp1.MeshData.textureCoordinatesLightMap.Length / 2;
            }

            //verticesNormals
            meshExpAppended.MeshData.verticesNormals = new float[mExp1.MeshData.verticesNormals.Length + mExp2.MeshData.verticesNormals.Length];
            Array.Copy(mExp1.MeshData.verticesNormals, 0, meshExpAppended.MeshData.verticesNormals, 0, mExp1.MeshData.verticesNormals.Length);
            Array.Copy(mExp2.MeshData.verticesNormals, 0, meshExpAppended.MeshData.verticesNormals, mExp1.MeshData.verticesNormals.Length, mExp2.MeshData.verticesNormals.Length);

            //Material
            if (mExp1.MeshRenderType == TgcMesh.MeshRenderType.VERTEX_COLOR)
            {
                meshExpAppended.diffuseMapsAbsolutePath = null;
                meshExpAppended.MaterialsData = null;
                meshExpAppended.MeshData.materialId = -1;
                meshExpAppended.MeshData.materialsIds = new int[0];
                meshExpAppended.MeshRenderType = TgcMesh.MeshRenderType.VERTEX_COLOR;
            }
            else if (mExp1.MeshRenderType == TgcMesh.MeshRenderType.DIFFUSE_MAP)
            {
                meshExpAppended.MeshRenderType = TgcMesh.MeshRenderType.DIFFUSE_MAP;

                //materialsIds: crear con la cantidad de triangulos total
                meshExpAppended.MeshData.materialsIds = new int[meshExpAppended.MeshData.coordinatesIndices.Length / 3];

                //copiar materialsIds del mesh 1, expandir si es necesario
                int triCountMesh1 = mExp1.MeshData.coordinatesIndices.Length / 3;
                int triCountMesh2 = mExp2.MeshData.coordinatesIndices.Length / 3;
                if (mExp1.MeshData.materialsIds.Length == 1)
                {
                    for (int i = 0; i < triCountMesh1; i++)
                    {
                        meshExpAppended.MeshData.materialsIds[i] = 0;
                    }
                }
                else
                {
                    Array.Copy(mExp1.MeshData.materialsIds, 0, meshExpAppended.MeshData.materialsIds, 0, mExp1.MeshData.materialsIds.Length);
                }

                //copiar materialsIds del mesh 2, expandir si es necesario
                if (mExp2.MeshData.materialsIds.Length == 1)
                {
                    for (int i = triCountMesh1; i < meshExpAppended.MeshData.materialsIds.Length; i++)
                    {
                        meshExpAppended.MeshData.materialsIds[i] = 0;
                    }
                }
                else
                {
                    Array.Copy(mExp2.MeshData.materialsIds, 0, meshExpAppended.MeshData.materialsIds, triCountMesh1, mExp2.MeshData.materialsIds.Length);
                }


                //Ver si tienen el mismo material
                List<MeshExport> unifiedMaterialsData = new List<MeshExport>();
                unifiedMaterialsData.Add(mExp1);
                int existingMatId = searchSameMaterial(mExp2, unifiedMaterialsData);

                //TODO: Con Multimaterial esta comparacion no anda muy bien. Habria que analizar Textura por Textura en vez de un todo o nada

                //Son materials identicos
                if (existingMatId == 0)
                {
                    //MaterialsData
                    meshExpAppended.MaterialsData = new TgcMaterialData[mExp1.MaterialsData.Length];
                    Array.Copy(mExp1.MaterialsData, 0, meshExpAppended.MaterialsData, 0, mExp1.MaterialsData.Length);

                    //paths absolutos de DiffuseMaps
                    meshExpAppended.diffuseMapsAbsolutePath = new string[mExp1.diffuseMapsAbsolutePath.Length];
                    Array.Copy(mExp1.diffuseMapsAbsolutePath, 0, meshExpAppended.diffuseMapsAbsolutePath, 0, mExp1.diffuseMapsAbsolutePath.Length);
                }

                //Juntar ambos materials en uno solo Multimaterial
                else
                {
                    //MaterialsData
                    meshExpAppended.MaterialsData = new TgcMaterialData[mExp1.MaterialsData.Length + mExp2.MaterialsData.Length];
                    Array.Copy(mExp1.MaterialsData, 0, meshExpAppended.MaterialsData, 0, mExp1.MaterialsData.Length);
                    Array.Copy(mExp2.MaterialsData, 0, meshExpAppended.MaterialsData, mExp1.MaterialsData.Length, mExp2.MaterialsData.Length);

                    //Aplicar offset a materialsIds del segundo Mesh
                    meshExpAppended.MeshData.materialId = 0;
                    for (int i = triCountMesh1; i < meshExpAppended.MeshData.materialsIds.Length; i++)
                    {
                        meshExpAppended.MeshData.materialsIds[i] += mExp1.MaterialsData.Length;
                    }

                    //Unificar paths absolutos de DiffuseMaps
                    meshExpAppended.diffuseMapsAbsolutePath = new string[mExp1.diffuseMapsAbsolutePath.Length + mExp2.diffuseMapsAbsolutePath.Length];
                    Array.Copy(mExp1.diffuseMapsAbsolutePath, 0, meshExpAppended.diffuseMapsAbsolutePath, 0, mExp1.diffuseMapsAbsolutePath.Length);
                    Array.Copy(mExp2.diffuseMapsAbsolutePath, 0, meshExpAppended.diffuseMapsAbsolutePath, mExp1.diffuseMapsAbsolutePath.Length, mExp2.diffuseMapsAbsolutePath.Length);
                }


            }

            return meshExpAppended;
        }


        /// <summary>
        /// Unifica todos los meshes en uno solo.
        /// Crea un nuevo mesh que tiene un merge de todos los demas.
        /// Toma el nombre, layer, userProperties, etc del primer mesh.
        /// Todos los meshes tienen que ser del mismo RenderType.
        /// No se puede hacer merge de meshes con Lightmap.
        /// No se hace dispose de los meshes originales
        /// </summary>
        /// <param name="meshes"></param>
        /// <returns></returns>
        public TgcMesh mergeMeshes(List<TgcMesh> meshes)
        {
            //Hay uno solo
            if (meshes.Count == 1)
            {
                return meshes[0].clone(meshes[0].Name);
            }

            //Hacer merge de a dos
            TgcMesh mesh1 = meshes[0];
            List<TgcMesh> mergedMeshes = new List<TgcMesh>();
            for (int i = 0; i < meshes.Count - 1; i++)
            {
                TgcMesh mergeMesh = mergeTwoMeshes(mesh1, meshes[i + 1]);
                mergedMeshes.Add(mergeMesh);
                mesh1 = mergeMesh;
            }

            //Hacer dispose de todos los merge intermedios
            TgcMesh finalMergeMesh = mergedMeshes[mergedMeshes.Count - 1];
            for (int i = 0; i < mergedMeshes.Count - 1; i++)
            {
                mergedMeshes[i].dispose();
            }
            mergedMeshes.Clear();


            return finalMergeMesh;
        }

        /// <summary>
        /// Une dos meshes en uno solo.
        /// Crea un nuevo mesh con un merge de los dos. Toma el nombre, layer, userProperties, etc del primer mesh.
        /// No se hace dispose de los dos meshes originales.
        /// Ambos mesh tienen que ser del mismo RenderType.
        /// No se puede hacer merge de un mesh con Lightmap
        /// </summary>
        /// <param name="mesh1">Primer mesh</param>
        /// <param name="mesh2">Segundo mesh</param>
        /// <returns>Nueve mesh con el merge de los dos</returns>
        public TgcMesh mergeTwoMeshes(TgcMesh mesh1, TgcMesh mesh2)
        {
            //Chequear que sea mismo tipo de malla
            if (mesh1.RenderType != mesh2.RenderType)
            {
                throw new Exception("Se intent� juntar dos Mallas de formato distintos: " + mesh1.Name + " y " + mesh2.Name);
            }

            //Por ahora no se pueden unificar LightMaps
            if (mesh1.RenderType == TgcMesh.MeshRenderType.DIFFUSE_MAP_AND_LIGHTMAP)
            {
                throw new Exception("Actualmente no esta soportado juntar dos Mallas que tienen LightMaps: " + mesh1.Name + " y " + mesh2.Name);
            }


            //Crear Mesh de D3D
            int triCount = mesh1.NumberTriangles + mesh2.NumberTriangles;
            int vertexCount = mesh1.NumberVertices + mesh2.NumberVertices;
            VertexElement[] vertexElements = mesh1.RenderType == TgcMesh.MeshRenderType.VERTEX_COLOR ? TgcSceneLoader.VertexColorVertexElements : TgcSceneLoader.DiffuseMapVertexElements;
            Mesh mesh = new Mesh(triCount, vertexCount, MeshFlags.Managed, vertexElements, GuiController.Instance.D3dDevice);

            //VertexColor
            if (mesh1.RenderType == TgcMesh.MeshRenderType.VERTEX_COLOR)
            {
                //Cargar VertexBuffer
                TgcSceneLoader.VertexColorVertex[] vertsData = new TgcSceneLoader.VertexColorVertex[vertexCount];
                //Agregar los datos del mesh1
                TgcSceneLoader.VertexColorVertex[] verts1 = (TgcSceneLoader.VertexColorVertex[])mesh1.D3dMesh.LockVertexBuffer(
                    typeof(TgcSceneLoader.VertexColorVertex), LockFlags.ReadOnly, mesh1.D3dMesh.NumberVertices);
                for (int i = 0; i < verts1.Length; i++)
                {
                    verts1[i].Position = TgcVectorUtils.transform(verts1[i].Position, mesh1.Transform);
                }
                Array.Copy(verts1, vertsData, verts1.Length);
                mesh1.D3dMesh.UnlockVertexBuffer();
                verts1 = null;

                //Agregar los datos del mesh1
                TgcSceneLoader.VertexColorVertex[] verts2 = (TgcSceneLoader.VertexColorVertex[])mesh2.D3dMesh.LockVertexBuffer(
                    typeof(TgcSceneLoader.VertexColorVertex), LockFlags.ReadOnly, mesh2.D3dMesh.NumberVertices);
                for (int i = 0; i < verts2.Length; i++)
                {
                    verts2[i].Position = TgcVectorUtils.transform(verts2[i].Position, mesh2.Transform);
                }
                Array.Copy(verts2, 0, vertsData, mesh1.NumberVertices, verts2.Length);
                mesh2.D3dMesh.UnlockVertexBuffer();
                verts2 = null;

                mesh.SetVertexBufferData(vertsData, LockFlags.None);
            }
            //DiffuseMap
            else if (mesh1.RenderType == TgcMesh.MeshRenderType.DIFFUSE_MAP)
            {
                //Cargar VertexBuffer
                TgcSceneLoader.DiffuseMapVertex[] vertsData = new TgcSceneLoader.DiffuseMapVertex[vertexCount];

                //Agregar los datos del mesh1 (aplicarle la transformacion actual)
                TgcSceneLoader.DiffuseMapVertex[] verts1 = (TgcSceneLoader.DiffuseMapVertex[])mesh1.D3dMesh.LockVertexBuffer(
                    typeof(TgcSceneLoader.DiffuseMapVertex), LockFlags.ReadOnly, mesh1.D3dMesh.NumberVertices);
                for (int i = 0; i < verts1.Length; i++)
                {
                    verts1[i].Position = TgcVectorUtils.transform(verts1[i].Position, mesh1.Transform);
                }
                Array.Copy(verts1, vertsData, verts1.Length);
                mesh1.D3dMesh.UnlockVertexBuffer();
                verts1 = null;

                //Agregar los datos del mesh1
                TgcSceneLoader.DiffuseMapVertex[] verts2 = (TgcSceneLoader.DiffuseMapVertex[])mesh2.D3dMesh.LockVertexBuffer(
                    typeof(TgcSceneLoader.DiffuseMapVertex), LockFlags.ReadOnly, mesh2.D3dMesh.NumberVertices);
                for (int i = 0; i < verts2.Length; i++)
                {
                    verts2[i].Position = TgcVectorUtils.transform(verts2[i].Position, mesh2.Transform);
                }
                Array.Copy(verts2, 0, vertsData, mesh1.NumberVertices, verts2.Length);
                mesh2.D3dMesh.UnlockVertexBuffer();
                verts2 = null;

                mesh.SetVertexBufferData(vertsData, LockFlags.None);
            }

            //Cargar indexBuffer en forma plana
            using (IndexBuffer ib = mesh.IndexBuffer)
            {
                short[] indices = new short[vertexCount];
                for (int i = 0; i < indices.Length; i++)
                {
                    indices[i] = (short)i;
                }
                ib.SetData(indices, 0, LockFlags.None);
            }


            //Cargar texturas y attributeBuffer
            TgcTexture[] textures = null;
            Material[] materials = null;
            if (mesh1.RenderType == TgcMesh.MeshRenderType.DIFFUSE_MAP)
            {
                //Cargar materials
                materials = new Material[mesh1.DiffuseMaps.Length + mesh2.DiffuseMaps.Length];
                int mIdx = 0;
                foreach (Material m in mesh1.Materials)
                {
                    materials[mIdx++] = m;
                }
                foreach (Material m in mesh2.Materials)
                {
                    materials[mIdx++] = m;
                }


                //Texturas del mesh1
                textures = new TgcTexture[mesh1.DiffuseMaps.Length + mesh2.DiffuseMaps.Length];
                int tIdx = 0;
                foreach (TgcTexture t in mesh1.DiffuseMaps)
                {
                    textures[tIdx++] = t.clone();
                }
                //Texturas del mesh2
                foreach (TgcTexture t in mesh2.DiffuseMaps)
                {
                    textures[tIdx++] = t.clone();
                }


                //Cargar el AttributeBuffer con la suma de ambos mesh
                int attIdx = 0;
                int textureId = 0;
                int[] attributeBuffer = mesh.LockAttributeBufferArray(LockFlags.None);

                //AttributeBuffer del mesh 1
                if (mesh1.DiffuseMaps.Length > 1)
                {
                    //Copiar el AttributeBuffer del mesh1 tal cual al mesh unificado
                    int[] attributeBuffer1 = mesh1.D3dMesh.LockAttributeBufferArray(LockFlags.ReadOnly);
                    Array.Copy(attributeBuffer1, attributeBuffer, attributeBuffer1.Length);
                    mesh1.D3dMesh.UnlockAttributeBuffer(attributeBuffer1);
                }
                else
                {
                    //Hay una sola textura, llenar el AttributeBuffer para que apunte solo a esa textura
                    for (int i = 0; i < mesh1.NumberTriangles; i++)
                    {
                        attributeBuffer[i] = textureId;
                    }
                }
                attIdx += mesh1.NumberTriangles;
                textureId += mesh1.DiffuseMaps.Length;

                //AttributeBuffer del mesh 2
                if (mesh2.DiffuseMaps.Length > 1)
                {
                    //Copiar el AttributeBuffer del mesh2 al mesh unificado pero sumando el offset de texturas del primero
                    int[] attributeBuffer2 = mesh2.D3dMesh.LockAttributeBufferArray(LockFlags.ReadOnly);
                    int[] attributeBuffer2Offset = new int[attributeBuffer2.Length];
                    for (int i = 0; i < attributeBuffer2.Length; i++)
                    {
                        attributeBuffer2Offset[i] = attributeBuffer2[i] + textureId;
                    }
                    mesh2.D3dMesh.UnlockAttributeBuffer(attributeBuffer2);
                    Array.Copy(attributeBuffer2Offset, 0, attributeBuffer, attIdx, attributeBuffer2Offset.Length);
                    attributeBuffer2Offset = null;
                }
                else
                {
                    //Hay una sola textura, llenar el AttributeBuffer para que apunte solo a esa textura
                    for (int i = 0; i < mesh2.NumberTriangles; i++)
                    {
                        attributeBuffer[attIdx++] = textureId;
                    }
                    textureId++;
                }

                mesh.UnlockAttributeBuffer(attributeBuffer);
            }
            

            //Crear mesh de TGC
            TgcMesh tgcMesh = new TgcMesh(mesh, mesh1.Name, mesh1.RenderType);
            tgcMesh.Layer = mesh1.Layer;
            tgcMesh.createBoundingBox();
            tgcMesh.Materials = materials;
            tgcMesh.DiffuseMaps = textures;
            tgcMesh.AlphaBlendEnable = mesh1.AlphaBlendEnable;
            tgcMesh.Enabled = mesh1.Enabled;

            //Transformaciones con la identidad (porque ya transformamos los vertices)
            tgcMesh.Position = new Vector3(0, 0, 0);
            tgcMesh.Rotation = new Vector3(0, 0, 0);
            tgcMesh.Scale = new Vector3(1, 1, 1);
            tgcMesh.Transform = Matrix.Identity;
            tgcMesh.AutoTransformEnable = mesh1.AutoTransformEnable;

            //Agregar userProperties de ambos
            if (mesh1.UserProperties != null || mesh2.UserProperties != null)
            {
                tgcMesh.UserProperties = new Dictionary<string, string>();
                if (mesh1.UserProperties != null)
                {
                    foreach (KeyValuePair<string, string> entry in mesh1.UserProperties)
                    {
                        tgcMesh.UserProperties.Add(entry.Key, entry.Value);
                    }
                }
                if (mesh2.UserProperties != null)
                {
                    foreach (KeyValuePair<string, string> entry in mesh2.UserProperties)
                    {
                        tgcMesh.UserProperties.Add(entry.Key, entry.Value);
                    }
                }
            }
            
            return tgcMesh;
        }


        #endregion


        #region Save to XML

        const string DEFAULT_TEXTURES_DIR = "Textures";
        const string DEFAULT_LIGHTMAPS_DIR = "LightMaps";
        

        /// <summary>
        /// Graba una escena a XML, en base a informaci�n de varios TgcMesh.
        /// Tambi�n crea una carpeta Textures relativa al XML y copia ah� todas las texturas utilizadas por las Mallas.
        /// Lo mismo para LightMaps.
        /// </summary>
        /// <param name="sceneName">Nombre de la escena</param>
        /// <param name="sceneBoundingBox">BoundingBox de toda la escena</param>
        /// <param name="meshesExport">Array de datos de Mallas que se quieren exportar</param>
        /// <param name="saveFolderPath">Carpeta en la que se quiera guardar el XML</param>
        /// <returns>Resultado de exportacion</returns>
        public ExportResult saveSceneToXml(string sceneName, TgcBoundingBox sceneBoundingBox, MeshExport[] meshesExport, string saveFolderPath)
        {
            ExportResult result = new ExportResult();
            try
            {
                //Ver si la escena tiene Lightmaps
                bool hasLightmaps = true;
                foreach (MeshExport mExp in meshesExport)
                {
                    if (mExp.MeshData.lightmap == null)
                    {
                        hasLightmaps = false;
                        break;
                    }
                }


                //Crear XML
                XmlDocument doc = new XmlDocument();
                XmlNode root = doc.CreateElement("tgcScene");

                //name
                XmlElement nameNode = doc.CreateElement("name");
                nameNode.InnerText = sceneName;
                root.AppendChild(nameNode);

                //texturesExport
                XmlElement texturesExportNode = doc.CreateElement("texturesExport");
                texturesExportNode.SetAttribute("enabled", true.ToString());
                texturesExportNode.SetAttribute("dir", DEFAULT_TEXTURES_DIR);
                root.AppendChild(texturesExportNode);

                //lightmapExport
                XmlElement lightmapExportNode = doc.CreateElement("lightmapExport");
                lightmapExportNode.SetAttribute("enabled", hasLightmaps.ToString());
                lightmapExportNode.SetAttribute("dir", DEFAULT_LIGHTMAPS_DIR);
                root.AppendChild(lightmapExportNode);

                //sceneBoundingBox
                XmlElement sceneBoundingBoxNode = doc.CreateElement("sceneBoundingBox");
                sceneBoundingBoxNode.SetAttribute("min", TgcParserUtils.printVector3(sceneBoundingBox.PMin));
                sceneBoundingBoxNode.SetAttribute("max", TgcParserUtils.printVector3(sceneBoundingBox.PMax));
                root.AppendChild(sceneBoundingBoxNode);

 
                //materials
                XmlElement materialsNode = doc.CreateElement("materials");
                List<MeshExport> unifiedMaterialsData = new List<MeshExport>();
                foreach (MeshExport mExp in meshesExport)
                {
                    if (mExp.MaterialsData != null)
                    {
                        //Buscar si ya exportamos un Material igual
                        int existingMatId = searchSameMaterial(mExp, unifiedMaterialsData);

                        //Nuevo Material
                        if (existingMatId == -1)
                        {
                            //Standardmaterial
                            if (mExp.MaterialsData.Length == 1)
                            {
                                //Crear Material
                                XmlElement mNode = createMaterialXmlNode(doc, mExp.MaterialsData[0], "m");
                                materialsNode.AppendChild(mNode);
                            }
                            //Multimaterial
                            else
                            {
                                //Crear Multimaterial
                                XmlElement mNode = doc.CreateElement("m");
                                mNode.SetAttribute("name", TgcMaterialData.MultiMaterial);
                                mNode.SetAttribute("type", TgcMaterialData.MultiMaterial);

                                //Crear SubMaterials
                                foreach (TgcMaterialData materialData in mExp.MaterialsData)
                                {
                                    XmlElement subMNode = createMaterialXmlNode(doc, materialData, "subM");
                                    mNode.AppendChild(subMNode);
                                }
                                materialsNode.AppendChild(mNode);
                            }

                            //Actualizar indice de material del mesh
                            unifiedMaterialsData.Add(mExp);
                            mExp.MeshData.materialId = unifiedMaterialsData.Count - 1;
                        }
                        //Material repetido
                        else
                        {
                            mExp.MeshData.materialId = existingMatId;
                        }
                        
                    }
                }
                materialsNode.SetAttribute("count", unifiedMaterialsData.Count.ToString());
                root.AppendChild(materialsNode);



                //meshes
                XmlElement meshesNode = doc.CreateElement("meshes");
                meshesNode.SetAttribute("count", meshesExport.Length.ToString());
                foreach (MeshExport mExp in meshesExport)
                {
                    TgcMeshData meshData = mExp.MeshData;

                    //mesh
                    XmlElement meshNode = doc.CreateElement("mesh");
                    meshNode.SetAttribute("name", meshData.name);
                    meshNode.SetAttribute("layer", meshData.layerName);
                    meshNode.SetAttribute("type", TgcMeshData.ORIGINAL);
                    meshNode.SetAttribute("matId", meshData.materialId.ToString());
                    meshNode.SetAttribute("color", meshData.color != null ? TgcParserUtils.printFloat3Array(meshData.color) : TgcParserUtils.printFloat3Array(new float[]{1,1,1}));
                    meshNode.SetAttribute("visibility", meshData.alphaBlending ? "0" : "1.0");
                    meshNode.SetAttribute("lightmap", meshData.lightmap != null ? meshData.lightmap : "");

                    //boundingBox
                    XmlElement boundingBoxNode = doc.CreateElement("boundingBox");
                    boundingBoxNode.SetAttribute("min", TgcParserUtils.printFloat3Array(meshData.pMin));
                    boundingBoxNode.SetAttribute("max", TgcParserUtils.printFloat3Array(meshData.pMax));
                    meshNode.AppendChild(boundingBoxNode);

                    //Malla original
                    if (meshData.instanceType.Equals(TgcMeshData.ORIGINAL))
                    {
                        //coordinatesIdx
                        XmlElement coordinatesIdxNode = doc.CreateElement("coordinatesIdx");
                        coordinatesIdxNode.SetAttribute("count", meshData.coordinatesIndices.Length.ToString());
                        coordinatesIdxNode.InnerText = TgcParserUtils.printIntStream(meshData.coordinatesIndices);
                        meshNode.AppendChild(coordinatesIdxNode);

                        //textCoordsIdx
                        XmlElement textCoordsIdxNode = doc.CreateElement("textCoordsIdx");
                        textCoordsIdxNode.SetAttribute("count", meshData.texCoordinatesIndices.Length.ToString());
                        textCoordsIdxNode.InnerText = TgcParserUtils.printIntStream(meshData.texCoordinatesIndices);
                        meshNode.AppendChild(textCoordsIdxNode);

                        //colorsIdx
                        XmlElement colorsIdxNode = doc.CreateElement("colorsIdx");
                        colorsIdxNode.SetAttribute("count", meshData.colorIndices.Length.ToString());
                        colorsIdxNode.InnerText = TgcParserUtils.printIntStream(meshData.colorIndices);
                        meshNode.AppendChild(colorsIdxNode);

                        //matIds
                        XmlElement matIdsNode = doc.CreateElement("matIds");
                        matIdsNode.SetAttribute("count", meshData.materialsIds.Length.ToString());
                        matIdsNode.InnerText = TgcParserUtils.printIntStream(meshData.materialsIds);
                        meshNode.AppendChild(matIdsNode);

                        //textCoordsLightMapIdx
                        XmlElement textCoordsLightMapIdxNode = doc.CreateElement("textCoordsLightMapIdx");
                        textCoordsLightMapIdxNode.SetAttribute("count", meshData.texCoordinatesIndicesLightMap.Length.ToString());
                        textCoordsLightMapIdxNode.InnerText = TgcParserUtils.printIntStream(meshData.texCoordinatesIndicesLightMap);
                        meshNode.AppendChild(textCoordsLightMapIdxNode);

                        //vertices
                        XmlElement verticesNode = doc.CreateElement("vertices");
                        verticesNode.SetAttribute("count", meshData.verticesCoordinates.Length.ToString());
                        verticesNode.InnerText = TgcParserUtils.printFloatStream(meshData.verticesCoordinates);
                        meshNode.AppendChild(verticesNode);

                        //normals
                        XmlElement normalsNode = doc.CreateElement("normals");
                        normalsNode.SetAttribute("count", meshData.verticesNormals.Length.ToString());
                        normalsNode.InnerText = TgcParserUtils.printFloatStream(meshData.verticesNormals);
                        meshNode.AppendChild(normalsNode);

                        //texCoords
                        XmlElement texCoordsNode = doc.CreateElement("texCoords");
                        texCoordsNode.SetAttribute("count", meshData.textureCoordinates.Length.ToString());
                        texCoordsNode.InnerText = TgcParserUtils.printFloatStream(meshData.textureCoordinates);
                        meshNode.AppendChild(texCoordsNode);

                        //colors
                        XmlElement colorsNode = doc.CreateElement("colors");
                        colorsNode.SetAttribute("count", meshData.verticesColors.Length.ToString());
                        colorsNode.InnerText = TgcParserUtils.printIntStream(meshData.verticesColors);
                        meshNode.AppendChild(colorsNode);

                        //texCoordsLightMap
                        XmlElement texCoordsLightMapNode = doc.CreateElement("texCoordsLightMap");
                        texCoordsLightMapNode.SetAttribute("count", meshData.textureCoordinatesLightMap.Length.ToString());
                        texCoordsLightMapNode.InnerText = TgcParserUtils.printFloatStream(meshData.textureCoordinatesLightMap);
                        meshNode.AppendChild(texCoordsLightMapNode);
                    }

                    //Malla instancia
                    else
                    {
                        meshNode.Attributes["type"].InnerText = TgcMeshData.INSTANCE;

                        //originalMesh
                        XmlElement originalMeshNode = doc.CreateElement("originalMesh");
                        originalMeshNode.InnerText = meshData.originalMesh.ToString();
                        meshNode.AppendChild(originalMeshNode);

                        //transform
                        XmlElement transformNode = doc.CreateElement("transform");
                        transformNode.SetAttribute("pos", TgcParserUtils.printFloat3Array(meshData.position));
                        transformNode.SetAttribute("rotQuat", TgcParserUtils.printFloat4Array(meshData.rotation));
                        transformNode.SetAttribute("scale", TgcParserUtils.printFloat3Array(meshData.scale));
                        meshNode.AppendChild(transformNode);
                    }

                    //userProps
                    if (meshData.userProperties != null)
                    {
                        XmlElement userPropsNode = doc.CreateElement("userProps");
                        userPropsNode.SetAttribute("count", meshData.userProperties.Count.ToString());
                        foreach (KeyValuePair<string,string> entry in meshData.userProperties)
                        {
                            XmlElement propNode = doc.CreateElement(entry.Key);
                            propNode.InnerText = entry.Value;
                            userPropsNode.AppendChild(propNode);
                        }
                        meshNode.AppendChild(userPropsNode);
                    }



                    meshesNode.AppendChild(meshNode);
                }
                root.AppendChild(meshesNode);


                //Guardar XML, borrar si ya existe
                doc.AppendChild(root);
                string sceneFileName = sceneName + "-TgcScene.xml";
                string sceneFilePath = saveFolderPath + "\\" + sceneFileName;
                result.filePath = sceneFilePath;
                if (File.Exists(sceneFilePath))
                {
                    File.Delete(sceneFilePath);
                }
                doc.Save(sceneFilePath);


                //Crear directorio de texturas
                string texturesDir = saveFolderPath + "\\" + DEFAULT_TEXTURES_DIR;
                if (!Directory.Exists(texturesDir))
                {
                    //Directory.Delete(texturesDir, true);
                    Directory.CreateDirectory(texturesDir);
                }

                //Copiar todos los DiffuseMap a la carpeta de texturas
                foreach (MeshExport mExp in meshesExport)
                {
                    if (mExp.diffuseMapsAbsolutePath != null)
                    {
                        for (int i = 0; i < mExp.diffuseMapsAbsolutePath.Length; i++)
                        {
                            try
                            {
                                copyFile(mExp.diffuseMapsAbsolutePath[i], texturesDir + "\\" + mExp.MaterialsData[i].fileName);
                            }
                            catch (Exception tEx)
                            {
                                result.secondaryErrors = true;
                                result.errors.Add("Error al copiar textura: " + mExp.diffuseMapsAbsolutePath[i] + ". " + tEx.Message);
                            }
                            
                        }
                    }
                }


                //Crear directorio de lightmaps, borrar si ya existe
                if (hasLightmaps)
                {
                    string lightmapsDir = saveFolderPath + "\\" + DEFAULT_LIGHTMAPS_DIR;
                    if (Directory.Exists(lightmapsDir))
                    {
                        Directory.Delete(lightmapsDir, true);
                    }
                    Directory.CreateDirectory(lightmapsDir);

                    //Copiar todos los lightmaps a la carpeta de lightmaps
                    foreach (MeshExport mExp in meshesExport)
                    {
                        try
                            {
                                copyFile(mExp.lightmapAbsolutePath, lightmapsDir + "\\" + mExp.MeshData.lightmap);
                            }
                            catch (Exception tEx)
                            {
                                result.secondaryErrors = true;
                                result.errors.Add("Error al copiar lightmap: " + mExp.lightmapAbsolutePath + ". " + tEx.Message);
                            }
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                result.result = false;
                result.errors.Add("Hubo un error inesperado al crear XML de escena: " + sceneName + ". " + ex.Message);
                return result;
            }
        }

        /// <summary>
        /// Copiar archivo
        /// </summary>
        private void copyFile(string oldPath, string newPath)
        {
            oldPath = oldPath.Replace("\\\\", "\\");
            newPath = newPath.Replace("\\\\", "\\");

            //Evitar copiar si el path de origen y destino es igual
            if (!oldPath.Equals(newPath))
            {
                File.Copy(oldPath, newPath, true);
            }   
        }

        /// <summary>
        /// Busca si existe un Material similar a este.
        /// Devuelve el indice encontrado o -1
        /// </summary>
        private int searchSameMaterial(MeshExport meshExport, List<MeshExport> unifiedMaterialsData)
        {
            for (int i = 0; i < unifiedMaterialsData.Count; i++)
            {
                MeshExport mExp = unifiedMaterialsData[i];
                if (mExp != meshExport && mExp.MaterialsData.Length == meshExport.MaterialsData.Length)
                {
                    //Standardmaterial
                    if (meshExport.MaterialsData.Length == 1)
                    {
                        if (equalsMaterial(mExp.MaterialsData[0], meshExport.MaterialsData[0]))
                        {
                            return i;
                        }
                    }
                    //Multimaterial
                    else
                    {
                        for (int j = 0; j < meshExport.MaterialsData.Length; j++)
                        {
                            if (!equalsMaterial(mExp.MaterialsData[j], meshExport.MaterialsData[j]))
                            {
                                return -1;
                            }
                        }
                        return i;
                    }
                }
            }
            return -1;
        }

        /// <summary>
        /// Indica si dos TgcMaterialData son iguales, en base a si utilizan la misma textura
        /// </summary>
        private bool equalsMaterial(TgcMaterialData m1, TgcMaterialData m2)
        {
            return m1.fileName == m2.fileName;
        }


        /// <summary>
        /// Crear nodo XML de Material
        /// </summary>
        private XmlElement createMaterialXmlNode(XmlDocument doc, TgcMaterialData materialData, string tagName)
        {
            //m o subM
            XmlElement mNode = doc.CreateElement(tagName);
            mNode.SetAttribute("name", materialData.name);
            mNode.SetAttribute("type", materialData.type);

            //ambient
            XmlElement ambientNode = doc.CreateElement("ambient");
            ambientNode.InnerText = TgcParserUtils.printFloat4Array(materialData.ambientColor);
            mNode.AppendChild(ambientNode);

            //diffuse
            XmlElement diffuseNode = doc.CreateElement("diffuse");
            diffuseNode.InnerText = TgcParserUtils.printFloat4Array(materialData.diffuseColor);
            mNode.AppendChild(diffuseNode);

            //specular
            XmlElement specularNode = doc.CreateElement("specular");
            specularNode.InnerText = TgcParserUtils.printFloat4Array(materialData.specularColor);
            mNode.AppendChild(specularNode);

            //opacity
            XmlElement opacityNode = doc.CreateElement("opacity");
            opacityNode.InnerText = TgcParserUtils.printFloat(materialData.opacity);
            mNode.AppendChild(opacityNode);

            //alphaBlendEnable
            XmlElement alphaBlendEnableNode = doc.CreateElement("alphaBlendEnable");
            alphaBlendEnableNode.InnerText = materialData.alphaBlendEnable.ToString();
            mNode.AppendChild(alphaBlendEnableNode);

            //bitmap
            XmlElement bitmapNode = doc.CreateElement("bitmap");
            bitmapNode.SetAttribute("uvTiling", TgcParserUtils.printFloat2Array(materialData.uvTiling));
            bitmapNode.SetAttribute("uvOffset", TgcParserUtils.printFloat2Array(materialData.uvOffset));
            bitmapNode.InnerText = materialData.fileName;
            mNode.AppendChild(bitmapNode);
            return mNode;
        }




        #endregion


    }
}
