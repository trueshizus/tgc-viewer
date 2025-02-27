﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TgcViewer.Utils.TgcSceneLoader;
using Microsoft.DirectX.Direct3D;
using Microsoft.DirectX;
using TgcViewer.Utils.TgcGeometry;
using TgcViewer.Utils.Input;
using TgcViewer;
using System.Drawing;
using Microsoft.DirectX.DirectInput;
using Examples.MeshCreator.EditablePolyTools.Primitives;

namespace Examples.MeshCreator.EditablePolyTools
{
    /// <summary>
    /// Herramienta para poder editar todos los componentes de un mesh: vertices, aristas y poligonos.
    /// </summary>
    public class EditablePoly
    {
        /// <summary>
        /// Estado dentro del modo EditablePoly
        /// </summary>
        public enum State
        {
            SelectObject,
            SelectingObject,
            TranslateGizmo,
        }

        /// <summary>
        /// Tipo de primitiva de EditablePoly
        /// </summary>
        public enum PrimitiveType
        {
            Vertex,
            Edge,
            Polygon,
            None
        }

        
        TgcMesh mesh;       
        bool dirtyValues;
        bool recreateMesh;
        bool selectiveObjectsAdditive;
        SelectionRectangleMesh rectMesh;
        Vector2 initMousePos;
        PrimitiveRenderer primitiveRenderer;
        EditablePolyTranslateGizmo translateGizmo;
        List<int> deletedTriangles;

        MeshCreatorControl control;
        /// <summary>
        /// Main Control
        /// </summary>
        public MeshCreatorControl Control
        {
            get { return control; }
        }

        State currentState;
        /// <summary>
        /// Estado actual
        /// </summary>
        public State CurrentState
        {
            get { return currentState; }
            set { currentState = value; }
        }

        List<EditPolyPrimitive> selectionList;
        /// <summary>
        /// Primitivas seleccionadas
        /// </summary>
        public List<EditPolyPrimitive> SelectionList
        {
            get { return selectionList; }
        }

        List<EditPolyVertex> vertices;
        /// <summary>
        /// Vertices
        /// </summary>
        public List<EditPolyVertex> Vertices
        {
            get { return vertices; }
        }

        List<EditPolyEdge> edges;
        /// <summary>
        /// Aristas
        /// </summary>
        public List<EditPolyEdge> Edges
        {
            get { return edges; }
        }

        List<EditPolyPolygon> polygons;
        /// <summary>
        /// Poligonos
        /// </summary>
        public List<EditPolyPolygon> Polygons
        {
            get { return polygons; }
        }

        short[] indexBuffer;
        /// <summary>
        /// Index buffer
        /// </summary>
        public short[] IndexBuffer
        {
            get { return indexBuffer; }
        }

        PrimitiveType currentPrimitive;
        /// <summary>
        /// Primitiva actual que se esta editando
        /// </summary>
        public PrimitiveType CurrentPrimitive
        {
            get { return currentPrimitive; }
        }

        /// <summary>
        /// Transform del mesh
        /// </summary>
        public Matrix Transform
        {
            get { return mesh.Transform; }
        }

        /// <summary>
        /// Construir un EditablePoly a partir de un mesh
        /// </summary>
        public EditablePoly(MeshCreatorControl control, TgcMesh origMesh)
        {
            this.control = control;
            this.currentPrimitive = PrimitiveType.None;
            this.rectMesh = new SelectionRectangleMesh();
            this.selectionList = new List<EditPolyPrimitive>();
            this.primitiveRenderer = new PrimitiveRenderer(this);
            this.translateGizmo = new EditablePolyTranslateGizmo(this);
            this.deletedTriangles = new List<int>();
            loadMesh(origMesh);
        }

        /// <summary>
        /// Cargar tipo de primitiva actual a editar
        /// </summary>
        public void setPrimitiveType(PrimitiveType p)
        {
            this.currentPrimitive = p;
            setSelectState();
        }

        /// <summary>
        /// Pasar a estado de seleccion de objetos
        /// </summary>
        public void setSelectState()
        {
            clearSelection();
            currentState = State.SelectObject;
        }

        /// <summary>
        /// Actualizacion de estado en render loop
        /// </summary>
        public void update()
        {
            //Procesar shorcuts de teclado
            processShortcuts();

            //Maquina de estados
            switch (currentState)
            {
                case State.SelectObject:
                    doSelectObject();
                    break;
                case State.SelectingObject:
                    doSelectingObject();
                    break;
                case State.TranslateGizmo:
                    doTranslateGizmo();
                    break;
            }
        }

        /// <summary>
        /// Procesar shorcuts de teclado
        /// </summary>
        private void processShortcuts()
        {
            TgcD3dInput input = GuiController.Instance.D3dInput;

            //Select
            if (input.keyPressed(Key.Q))
            {
                setSelectState();
            }
            //Select all
            else if (input.keyDown(Key.LeftControl) && input.keyPressed(Key.E))
            {
                selectAll();
            }
            //Delete
            else if (input.keyPressed(Key.Delete))
            {
                deleteSelectedPrimitives();
            }
            //Translate
            else if (input.keyPressed(Key.W))
            {
                activateTranslateGizmo();
            }
            //Zoom
            else if (input.keyPressed(Key.Z))
            {
                EditablePolyUtils.zoomPrimitives(control.Camera, selectionList, Transform);
            }
            //Top view
            else if (input.keyPressed(Key.T))
            {
                EditablePolyUtils.setCameraTopView(control.Camera, selectionList, Transform);
            }
            //Left view
            else if (input.keyPressed(Key.L))
            {
                EditablePolyUtils.setCameraLeftView(control.Camera, selectionList, Transform);
            }
            //Front view
            else if (input.keyPressed(Key.F))
            {
                EditablePolyUtils.setCameraFrontView(control.Camera, selectionList, Transform);
            }
        }

        

        



        #region Primitive selection
       

        /// <summary>
        /// Estado: seleccionar objetos (estado default)
        /// </summary>
        private void doSelectObject()
        {
            TgcD3dInput input = GuiController.Instance.D3dInput;

            //Si mantiene control y clic con el mouse, iniciar cuadro de seleccion para agregar/quitar a la seleccion actual
            if ((input.keyDown(Microsoft.DirectX.DirectInput.Key.LeftControl) || input.keyDown(Microsoft.DirectX.DirectInput.Key.RightControl))
                && input.buttonDown(TgcD3dInput.MouseButtons.BUTTON_LEFT))
            {
                currentState = State.SelectingObject;
                this.initMousePos = new Vector2(input.Xpos, input.Ypos);
                this.selectiveObjectsAdditive = true;
            }
            //Si mantiene el clic con el mouse, iniciar cuadro de seleccion
            else if (input.buttonDown(TgcD3dInput.MouseButtons.BUTTON_LEFT))
            {
                currentState = State.SelectingObject;
                this.initMousePos = new Vector2(input.Xpos, input.Ypos);
                this.selectiveObjectsAdditive = false;
            }
        }

        /// <summary>
        /// Estado: Cuando se esta arrastrando el mouse para armar el cuadro de seleccion
        /// </summary>
        private void doSelectingObject()
        {
            TgcD3dInput input = GuiController.Instance.D3dInput;

            //Mantiene el mouse apretado
            if (input.buttonDown(TgcD3dInput.MouseButtons.BUTTON_LEFT))
            {
                //Definir recuadro
                Vector2 mousePos = new Vector2(input.Xpos, input.Ypos);
                Vector2 min = Vector2.Minimize(initMousePos, mousePos);
                Vector2 max = Vector2.Maximize(initMousePos, mousePos);

                rectMesh.updateMesh(min, max);

            }
            //Solo el mouse
            else if (input.buttonUp(TgcD3dInput.MouseButtons.BUTTON_LEFT))
            {
                //Definir recuadro
                Vector2 mousePos = new Vector2(input.Xpos, input.Ypos);
                Vector2 min = Vector2.Minimize(initMousePos, mousePos);
                Vector2 max = Vector2.Maximize(initMousePos, mousePos);
                Rectangle r = new Rectangle((int)min.X, (int)min.Y, (int)(max.X - min.X), (int)(max.Y - min.Y));

                //Usar recuadro de seleccion solo si tiene un tamaño minimo
                if (r.Width > 1 && r.Height > 1)
                {
                    //Limpiar seleccionar anterior si no estamos agregando en forma aditiva
                    if (!selectiveObjectsAdditive)
                    {
                        clearSelection();
                    }

                    //Buscar que primitivas caen dentro de la seleccion y elegirlos
                    int i = 0;
                    EditPolyPrimitive p = iteratePrimitive(currentPrimitive, i);
                    while (p != null)
                    {
                        //Ver si hay colision contra la proyeccion de la primitiva y el rectangulo 2D
                        Rectangle primRect;
                        if (p.projectToScreen(mesh.Transform, out primRect))
                        {
                            if (r.IntersectsWith(primRect))
                            {
                                //Agregar el objeto en forma aditiva
                                if (selectiveObjectsAdditive)
                                {
                                    selectOrRemovePrimitiveIfPresent(p);
                                }
                                //Agregar el objeto en forma simple
                                else
                                {
                                    selectPrimitive(p);
                                }
                            }
                        }
                        p = iteratePrimitive(currentPrimitive, ++i);
                    }
                }
                //Si el recuadro no tiene tamaño suficiente, hacer seleccion directa
                else
                {
                    doDirectSelection(selectiveObjectsAdditive);
                }

                currentState = State.SelectObject;

                /*
                //Si quedo algo seleccionado activar gizmo
                if (selectionList.Count > 0)
                {
                    activateTranslateGizmo();
                }
                */

                //Actualizar panel de Modify con lo que se haya seleccionado, o lo que no
                updateUiPanel();
            }



            //Dibujar recuadro
            rectMesh.render();
        }

        /// <summary>
        /// Actualizar datos de UI de EditablePoly
        /// </summary>
        public void updateUiPanel()
        {
            control.IgnoreChangeEvents = true;

            //Modificacion de textura en poligonos
            if (selectionList.Count > 0 && mesh.DiffuseMaps.Length > 0 && currentPrimitive == PrimitiveType.Polygon)
            {
                EditPolyPolygon firstPolygon = (EditPolyPolygon)selectionList[0];
                control.numericUpDownEPolyTextureNumber.Enabled = true;
                control.numericUpDownEPolyTextureNumber.Minimum = 1;
                control.numericUpDownEPolyTextureNumber.Maximum = mesh.DiffuseMaps.Length;
                control.numericUpDownEPolyTextureNumber.Value = firstPolygon.matId + 1;
                control.pictureBoxEPolyTexture.Enabled = true;
                if (control.pictureBoxEPolyTexture.Image != null)
                {
                    control.pictureBoxEPolyTexture.Image.Dispose();
                }
                string imagePath = mesh.DiffuseMaps[firstPolygon.matId].FilePath;
                control.pictureBoxEPolyTexture.Image = Image.FromFile(imagePath);
                control.pictureBoxEPolyTexture.ImageLocation = imagePath;

                control.buttonEPolyAddTexture.Enabled = true;
                control.buttonEPolyDeleteTexture.Enabled = mesh.DiffuseMaps.Length > 1 ? true : false;
            }
            else
            {
                control.numericUpDownEPolyTextureNumber.Enabled = false;
                control.pictureBoxEPolyTexture.Enabled = false;
                control.buttonEPolyAddTexture.Enabled = false;
                control.buttonEPolyDeleteTexture.Enabled = false;
            }

            control.IgnoreChangeEvents = false;
        }

        

        /// <summary>
        /// Seleccionar una sola primitiva
        /// </summary>
        private void selectPrimitive(EditPolyPrimitive p)
        {
            p.Selected = true;
            selectionList.Add(p);
        }

        /// <summary>
        /// Selecciona una sola primitiva pero antes se fija si ya no estaba en la lista de seleccion.
        /// Si ya estaba, entonces la quita de la lista de seleccion
        /// </summary>
        private void selectOrRemovePrimitiveIfPresent(EditPolyPrimitive p)
        {
            //Ya existe, quitar
            if (selectionList.Contains(p))
            {
                p.Selected = false;
                selectionList.Remove(p);
            }
            //No existe, agregar
            else
            {
                p.Selected = true;
                selectionList.Add(p);
            }
        }

        /// <summary>
        /// Deseleccionar todo
        /// </summary>
        private void clearSelection()
        {
            foreach (EditPolyPrimitive p in selectionList)
            {
                p.Selected = false;
            }
            selectionList.Clear();
            updateUiPanel();
        }

        /// <summary>
        /// Seleccionar todo
        /// </summary>
        public void selectAll()
        {
            clearSelection();
            int i = 0;
            EditPolyPrimitive p = iteratePrimitive(currentPrimitive, i);
            while (p != null)
            {
                selectPrimitive(p);
                p = iteratePrimitive(currentPrimitive, ++i);
            }
            updateUiPanel();
        }

        /// <summary>
        /// Hacer picking para seleccionar la primitiva mas cercano del ecenario.
        /// </summary>
        /// <param name="additive">En True agrega/quita la primitiva a la seleccion actual</param>
        public void doDirectSelection(bool additive)
        {
            control.PickingRay.updateRay();

            //Buscar menor colision con primitivas
            float minDistSq = float.MaxValue;
            EditPolyPrimitive closestPrimitive = null;
            Vector3 q;
            int i = 0;
            EditPolyPrimitive p = iteratePrimitive(currentPrimitive, i);
            while (p != null)
            {
                if (p.intersectRay(control.PickingRay.Ray, mesh.Transform, out q))
                {
                    float lengthSq = Vector3.Subtract(control.PickingRay.Ray.Origin, q).LengthSq();
                    if (lengthSq < minDistSq)
                    {
                        minDistSq = lengthSq;
                        closestPrimitive = p;
                    }
                }
                p = iteratePrimitive(currentPrimitive, ++i);
            }

            //Agregar
            if (closestPrimitive != null)
            {
                //Sumar a la lista de seleccion
                if (additive)
                {
                    selectOrRemovePrimitiveIfPresent(closestPrimitive);
                }
                //Seleccionar uno solo
                else
                {
                    clearSelection();
                    selectPrimitive(closestPrimitive);
                }
                //activateTranslateGizmo();
            }
            //Nada seleccionado
            else
            {
                //Limpiar seleccion
                clearSelection();
            }

            //Pasar a modo seleccion
            currentState = State.SelectObject;
            updateUiPanel();
        }


        #endregion


        #region Translate Gizmo

        /// <summary>
        /// Activar gizmo de translate
        /// </summary>
        public void activateTranslateGizmo()
        {
            translateGizmo.setEnabled(true);
        }

        /// <summary>
        /// Actualizar eventos de gizmo de translate
        /// </summary>
        private void doTranslateGizmo()
        {
            if (selectionList.Count > 0)
            {
                translateGizmo.update();
            }
        }

        #endregion


        /// <summary>
        /// Marcar mesh como dirty para regenerar en el proximo cuadro
        /// </summary>
        public void setDirtyValues(bool recreateMesh)
        {
            this.dirtyValues = true;
            this.recreateMesh = recreateMesh;
        }

        /// <summary>
        /// Iterar sobre lista de primitivas
        /// </summary>
        /// <param name="primitiveType">Primitiva</param>
        /// <param name="i">indice</param>
        /// <returns>Elemento o null si no hay mas</returns>
        private EditPolyPrimitive iteratePrimitive(PrimitiveType primitiveType, int i)
        {
            switch (primitiveType)
            {
                case PrimitiveType.Vertex:
                    if (i == vertices.Count) return null;
                    return vertices[i];
                case PrimitiveType.Edge:
                    if (i == edges.Count) return null;
                    return edges[i];
                case PrimitiveType.Polygon:
                    if (i == polygons.Count) return null;
                    return polygons[i];
            }
            return null;
        }

        /// <summary>
        /// Dibujar
        /// </summary>
        public void render()
        {
            //Actualizar mesh si hubo algun cambio
            if (dirtyValues)
            {
                updateMesh();
                dirtyValues = false;
                recreateMesh = false;
            }

            //Render de mesh
            mesh.render();

            //Render de primitivas
            primitiveRenderer.render(mesh.Transform);

            //Translate gizmo
            if (currentState == State.TranslateGizmo)
            {
                translateGizmo.render();
            }
        }

        /// <summary>
        /// Liberar recursos
        /// </summary>
        public void dispose()
        {
            control = null;
            primitiveRenderer.dispose();
        }

        /// <summary>
        /// Cambiar ID de textura de los poligonos seleccionados
        /// </summary>
        public void changeTextureId(int n)
        {
            foreach (EditPolyPolygon p in selectionList)
            {
                p.matId = n;
            }
            setDirtyValues(false);
        }

        /// <summary>
        /// Se elimina un ID de textura y se reemplaza por otro id.
        /// Ajusta todos los matId de los poligonos
        /// </summary>
        public void deleteTextureId(int id, int replacementId)
        {
            foreach (EditPolyPolygon p in polygons)
            {
                if (p.matId == id)
                {
                    p.matId = replacementId;
                }
                else if (p.matId > id)
                {
                    p.matId--;
                }
            }
        }


        #region Delete primitive

        /// <summary>
        /// Borrar las primitivas seleccionadas
        /// </summary>
        public void deleteSelectedPrimitives()
        {
            foreach (EditPolyPrimitive p in selectionList)
            {
                switch (p.Type)
                {
                    case PrimitiveType.Vertex:
                        deleteVertex((EditPolyVertex)p);
                        break;
                    case PrimitiveType.Edge:
                        deleteEdge((EditPolyEdge)p);
                        break;
                    case PrimitiveType.Polygon:
                        deletePolygon((EditPolyPolygon)p);
                        break;
                }
            }

            if(vertices.Count > 0)
            {
                //setear para crear nuevo mesh
                setDirtyValues(true);
                clearSelection();
                currentState = State.SelectObject;
            }
            else
            {
                //No quedo nada del mesh. Eliminarlo del editor
                control.setEditablePolyEnable(false, PrimitiveType.None);
                control.deleteSelectedObjects();
            }
            
        }

        

        /// <summary>
        /// Eliminar un vertice
        /// </summary>
        private void deleteVertex(EditPolyVertex v)
        {
            //Quitar referencia de todas las aristas que lo usan
            foreach (EditPolyEdge edge in v.edges)
            {
                edge.a = null;
                edge.b = null;
            }

            //Eliminar aristas
            foreach (EditPolyEdge edge in v.edges)
            {
                deleteEdge(edge);
            }

            //Quitar vertice de lista de vertices
            vertices.RemoveAt(v.vbIndex);

            //Shift de vertex buffer
            for (int i = 0; i < vertices.Count; i++)
            {
                vertices[i].vbIndex = i;
            }

            //Ajustar indices en index buffer
            for (int i = 0; i < indexBuffer.Length; i++)
            {
                if (indexBuffer[i] >= v.vbIndex)
                {
                    indexBuffer[i]--;
                }
            }
        }

        /// <summary>
        /// Eliminar una arista
        /// </summary>
        private void deleteEdge(EditPolyEdge e)
        {
            //Quitar referencia de todos los poligonos
            foreach (EditPolyPolygon poly in e.faces)
            {
                poly.removeEdge(e);
            }

            //Eliminar poligonos
            foreach (EditPolyPolygon poly in e.faces)
            {
                deletePolygon(poly);
            }

            //Quitar referencia a vertices y eliminar si quedaron aislados
            if (e.a != null)
            {
                e.a.removeEdge(e);
                if (e.a.edges.Count == 0)
                {
                    deleteVertex(e.a);
                }
            }
            if (e.b != null)
            {
                e.b.removeEdge(e);
                if (e.b.edges.Count == 0)
                {
                    deleteVertex(e.b);
                }
            }

            //Quitar de lista de aristas
            for (int i = 0; i < edges.Count; i++)
            {
                if(edges[i] == e)
                {
                    edges.RemoveAt(i);
                    break;
                }
            }
        }

        /// <summary>
        /// Eliminar poligono
        /// </summary>
        private void deletePolygon(EditPolyPolygon p)
        {
            //Quitar triangulos de index buffer
            int vertexToDelete = p.vbTriangles.Count * 3;
            short[] newIndexBuffer = new short[indexBuffer.Length - vertexToDelete];
            int w = 0;
            for (int i = 0; i < indexBuffer.Length; i += 3)
            {
                bool toDelete = false;
                for (int j = 0; j < p.vbTriangles.Count; j++)
                {
                    if (i == p.vbTriangles[j])
                    {
                        toDelete = true;
                        break;
                    }
                }
                if(toDelete)
                {
                    //Ajustar todos los indices de triangulos del resto de los poligonos existentes
                    foreach (EditPolyPolygon poly in polygons)
                    {
                        if(poly != p)
                        {
                            for (int j = 0; j < poly.vbTriangles.Count; j++)
                            {
                                if(poly.vbTriangles[j] >= i)
                                {
                                    poly.vbTriangles[j] -= 3;
                                }
                            }
                        }
                    }
                }
                else
                {
                    newIndexBuffer[w++] = indexBuffer[i];
                    newIndexBuffer[w++] = indexBuffer[i + 1];
                    newIndexBuffer[w++] = indexBuffer[i + 2];
                }
            }
            indexBuffer = newIndexBuffer;

            //Quitar referencia a aristas
            foreach (EditPolyEdge edge in p.edges)
            {
                edge.removePolygon(p);
            }

            //Eliminar aristas que quedaron aisladas
            foreach (EditPolyEdge edge in p.edges)
            {
                if (edge.faces.Count == 0)
                {
                    deleteEdge(edge);
                }
            }

            //Quitar de lista de poligonos
            for (int i = 0; i < polygons.Count; i++)
            {
                if (polygons[i] == p)
                {
                    polygons.RemoveAt(i);
                    break;
                }
            }

            //Registrar triangulos que se eliminaron (apuntando al indice del primer vertice de cada triangulo)
            //Se registran el valor original al que apuntaba el triangulo antes de haber borrado nada
            int currentDeletedCount = deletedTriangles.Count;
            for (int i = 0; i < p.vbTriangles.Count; i++)
            {
                int triDelIdx = p.vbTriangles[i];
                for (int j = 0; j < currentDeletedCount; j++)
                {
                    if (triDelIdx < deletedTriangles[j])
                    {
                        break;
                    }
                    triDelIdx += 3;
                }
                deletedTriangles.Add(triDelIdx);
            }
            deletedTriangles.Sort();
        }

        


        #endregion



        #region Mesh loading

        /// <summary>
        /// Tomar un mesh cargar todas las estructuras internas necesarias para poder editarlo
        /// </summary>
        private void loadMesh(TgcMesh origMesh)
        {
            //Obtener vertices del mesh
            this.mesh = origMesh;
            List<EditPolyVertex> origVertices = getMeshOriginalVertexData(origMesh);
            int origTriCount = origVertices.Count / 3;

            //Iterar sobre los triangulos y generar data auxiliar unificada
            vertices = new List<EditPolyVertex>();
            edges = new List<EditPolyEdge>();
            polygons = new List<EditPolyPolygon>();
            indexBuffer = new short[origTriCount * 3];
            int[] attributeBuffer = origMesh.D3dMesh.LockAttributeBufferArray(LockFlags.ReadOnly);
            origMesh.D3dMesh.UnlockAttributeBuffer(attributeBuffer);
            for (int i = 0; i < origTriCount; i++)
            {
                EditPolyVertex v1 = origVertices[i * 3];
                EditPolyVertex v2 = origVertices[i * 3 + 1];
                EditPolyVertex v3 = origVertices[i * 3 + 2];

                //Agregar vertices a la lista, si es que son nuevos
                int v1Idx = EditablePolyUtils.addVertexToListIfUnique(vertices, v1);
                int v2Idx = EditablePolyUtils.addVertexToListIfUnique(vertices, v2);
                int v3Idx = EditablePolyUtils.addVertexToListIfUnique(vertices, v3);
                v1 = vertices[v1Idx];
                v2 = vertices[v2Idx];
                v3 = vertices[v3Idx];

                //Crear edges
                EditPolyEdge e1 = new EditPolyEdge();
                e1.a = v1;
                e1.b = v2;
                EditPolyEdge e2 = new EditPolyEdge();
                e2.a = v2;
                e2.b = v3;
                EditPolyEdge e3 = new EditPolyEdge();
                e3.a = v3;
                e3.b = v1;

                //Crear poligono para este triangulo
                EditPolyPolygon p = new EditPolyPolygon();
                p.vertices = new List<EditPolyVertex>();
                p.vertices.Add(v1);
                p.vertices.Add(v2);
                p.vertices.Add(v3);
                p.edges = new List<EditPolyEdge>();
                p.edges.Add(e1);
                p.edges.Add(e2);
                p.edges.Add(e3);
                p.vbTriangles = new List<int>();
                p.vbTriangles.Add(i * 3);
                p.plane = Plane.FromPoints(v1.position, v2.position, v3.position);
                p.plane.Normalize();
                p.matId = attributeBuffer[i];

                //Agregar triangulo al index buffer
                indexBuffer[i * 3] = (short)v1Idx;
                indexBuffer[i * 3 + 1] = (short)v2Idx;
                indexBuffer[i * 3 + 2] = (short)v3Idx;

                //Agregar a lista de poligonos
                polygons.Add(p);


                /*
                //Buscar si hay un poligono ya existente al cual sumarnos (coplanar y que compartan una arista)
                EditPolyPolygon coplanarP = null;
                for (int j = 0; j < polygons.Count; j++)
                {
                    //Coplanares y con igual material ID
                    EditPolyPolygon p0 = polygons[j];
                    if (p0.matId == p.matId && EditablePolyUtils.samePlane(p0.plane, p.plane))
                    {
                        //Buscar si tienen una arista igual
                        int p0SharedEdgeIdx;
                        int pSharedEdgeIdx;
                        if (EditablePolyUtils.findShareEdgeBetweenPolygons(p0, p, out p0SharedEdgeIdx, out pSharedEdgeIdx))
                        {
                            //Obtener el tercer vertice del triangulo que no es parte de la arista compartida
                            EditPolyEdge sharedEdge = p0.edges[p0SharedEdgeIdx];
                            EditPolyVertex thirdVert;
                            if (p.vertices[0] != sharedEdge.a && p.vertices[0] != sharedEdge.b)
                                thirdVert = p.vertices[0];
                            else if (p.vertices[1] != sharedEdge.a && p.vertices[1] != sharedEdge.b)
                                thirdVert = p.vertices[1];
                            else
                                thirdVert = p.vertices[2];

                            //Agregar el tercer vertice al poligno existente
                            EditablePolyUtils.addVertexToPolygon(p0, sharedEdge, thirdVert);

                            //Quitar arista compartida
                            p0.edges.Remove(sharedEdge);

                            //Agregar al poligono dos nuevas aristas que conectar los extremos de la arista compartida hacia el tercer vertice
                            EditPolyEdge newPolEdge1 = new EditPolyEdge();
                            newPolEdge1.a = sharedEdge.a;
                            newPolEdge1.b = thirdVert;
                            p0.edges.Add(newPolEdge1);

                            EditPolyEdge newPolEdge2 = new EditPolyEdge();
                            newPolEdge2.a = thirdVert;
                            newPolEdge2.b = sharedEdge.b;
                            p0.edges.Add(newPolEdge2);

                            //Agregar indice de triangulo del vertexBuffer que se sumo al poligono
                            p0.vbTriangles.Add(p.vbTriangles[0]);

                            coplanarP = p0;
                        }
                    }
                }
                //Es un nuevo poligono, agregarlo
                if (coplanarP == null)
                {
                    polygons.Add(p);
                }
                 */ 
            }


            //Unificar aristas de los poligonos
            foreach (EditPolyPolygon p in polygons)
            {
                for (int i = 0; i < p.edges.Count; i++)
                {
                    bool newEdgeAdded;
                    int eIdx = EditablePolyUtils.addEdgeToListIfUnique(edges, p.edges[i], out newEdgeAdded);
                    EditPolyEdge e = edges[eIdx];
                    
                    //Nueva arista incorporada a la lista
                    if (newEdgeAdded)
                    {
                        e.faces = new List<EditPolyPolygon>();

                        //Agregar referencia a vertices que usan la arista
                        e.a.edges.Add(e);
                        e.b.edges.Add(e);
                    }
                    //Se usa arista existente de la lista
                    else
                    {
                        //Reemplazar en poligono por la nueva
                        p.edges[i] = e;
                    }

                    //Indicar a la arista que pertenece al poligono actual
                    e.faces.Add(p);
                }
            }
            

            setDirtyValues(false);
        }

        /// <summary>
        /// Obtener la lista de vertices originales del mesh
        /// </summary>
        private List<EditPolyVertex> getMeshOriginalVertexData(TgcMesh origMesh)
        {
            List<EditPolyVertex> origVertices = new List<EditPolyVertex>();
            switch (origMesh.RenderType)
            {
                case TgcMesh.MeshRenderType.VERTEX_COLOR:
                    TgcSceneLoader.VertexColorVertex[] verts1 = (TgcSceneLoader.VertexColorVertex[])origMesh.D3dMesh.LockVertexBuffer(
                        typeof(TgcSceneLoader.VertexColorVertex), LockFlags.ReadOnly, origMesh.D3dMesh.NumberVertices);
                    for (int i = 0; i < verts1.Length; i++)
                    {
                        EditPolyVertex v = new EditPolyVertex();
                        v.position = verts1[i].Position;
                        /*v.normal = verts1[i].Normal;
                        v.color = verts1[i].Color;*/
                        origVertices.Add(v);
                    }
                    origMesh.D3dMesh.UnlockVertexBuffer();
                    break;

                case TgcMesh.MeshRenderType.DIFFUSE_MAP:
                    TgcSceneLoader.DiffuseMapVertex[] verts2 = (TgcSceneLoader.DiffuseMapVertex[])origMesh.D3dMesh.LockVertexBuffer(
                        typeof(TgcSceneLoader.DiffuseMapVertex), LockFlags.ReadOnly, origMesh.D3dMesh.NumberVertices);
                    for (int i = 0; i < verts2.Length; i++)
                    {
                        EditPolyVertex v = new EditPolyVertex();
                        v.position = verts2[i].Position;
                        /*v.normal = verts2[i].Normal;
                        v.texCoords = new Vector2(verts2[i].Tu, verts2[i].Tv);
                        v.color = verts2[i].Color;*/
                        origVertices.Add(v);
                    }
                    origMesh.D3dMesh.UnlockVertexBuffer();
                    break;

                case TgcMesh.MeshRenderType.DIFFUSE_MAP_AND_LIGHTMAP:
                    TgcSceneLoader.DiffuseMapAndLightmapVertex[] verts3 = (TgcSceneLoader.DiffuseMapAndLightmapVertex[])origMesh.D3dMesh.LockVertexBuffer(
                        typeof(TgcSceneLoader.DiffuseMapAndLightmapVertex), LockFlags.ReadOnly, origMesh.D3dMesh.NumberVertices);
                    for (int i = 0; i < verts3.Length; i++)
                    {
                        EditPolyVertex v = new EditPolyVertex();
                        v.position = verts3[i].Position;
                        /*v.normal = verts3[i].Normal;
                        v.texCoords = new Vector2(verts3[i].Tu0, verts3[i].Tv0);
                        v.color = verts3[i].Color;
                        v.texCoords2 = new Vector2(verts3[i].Tu1, verts3[i].Tv1);*/
                        origVertices.Add(v);
                    }
                    origMesh.D3dMesh.UnlockVertexBuffer();
                    break;
            }

            return origVertices;
        }

        /// <summary>
        /// Actualizar vertexBuffer de mesh original en base a la estructura interna del editablePoly
        /// </summary>
        private void updateMesh()
        {
            
            //Cambio la estructura interna del mesh, crear uno nuevo
            if (recreateMesh)
            {
                //Crear nuevo mesh con una cantidad distinta de triangulos y vertices
                Mesh newD3dMesh = null;
                int triCount = indexBuffer.Length / 3;
                int vertCount = indexBuffer.Length;
                int w = 0;
                int delTriIdx = 0;
                switch (mesh.RenderType)
                {
                    case TgcMesh.MeshRenderType.VERTEX_COLOR:
                        newD3dMesh = new Mesh(triCount, vertCount, MeshFlags.Managed, TgcSceneLoader.VertexColorVertexElements, GuiController.Instance.D3dDevice);
                        TgcSceneLoader.VertexColorVertex[] origVert1 = (TgcSceneLoader.VertexColorVertex[])mesh.D3dMesh.LockVertexBuffer(
                            typeof(TgcSceneLoader.VertexColorVertex), LockFlags.ReadOnly, mesh.D3dMesh.NumberVertices);
                        mesh.D3dMesh.UnlockVertexBuffer();
                        TgcSceneLoader.VertexColorVertex[] newVert1 = (TgcSceneLoader.VertexColorVertex[])newD3dMesh.LockVertexBuffer(
                            typeof(TgcSceneLoader.VertexColorVertex), LockFlags.None, newD3dMesh.NumberVertices);
                        for (int i = 0; i < origVert1.Length; i += 3)
                        {
                            if (delTriIdx < deletedTriangles.Count && i == deletedTriangles[delTriIdx])
                            {
                                delTriIdx++;
                            }
                            else
                            {
                                newVert1[w++] = origVert1[i];
                                newVert1[w++] = origVert1[i + 1];
                                newVert1[w++] = origVert1[i + 2];
                            }
                        }
                        mesh.D3dMesh.UnlockVertexBuffer();
                        newD3dMesh.SetVertexBufferData(newVert1, LockFlags.None);
                        newD3dMesh.UnlockVertexBuffer();
                        break;
                    case TgcMesh.MeshRenderType.DIFFUSE_MAP:
                        newD3dMesh = new Mesh(triCount, vertCount, MeshFlags.Managed, TgcSceneLoader.DiffuseMapVertexElements, GuiController.Instance.D3dDevice);
                        TgcSceneLoader.DiffuseMapVertex[] origVert2 = (TgcSceneLoader.DiffuseMapVertex[])mesh.D3dMesh.LockVertexBuffer(
                            typeof(TgcSceneLoader.DiffuseMapVertex), LockFlags.ReadOnly, mesh.D3dMesh.NumberVertices);
                        mesh.D3dMesh.UnlockVertexBuffer();
                        TgcSceneLoader.DiffuseMapVertex[] newVert2 = (TgcSceneLoader.DiffuseMapVertex[])newD3dMesh.LockVertexBuffer(
                            typeof(TgcSceneLoader.DiffuseMapVertex), LockFlags.None, newD3dMesh.NumberVertices);
                        for (int i = 0; i < origVert2.Length; i += 3)
                        {
                            if (delTriIdx < deletedTriangles.Count && i == deletedTriangles[delTriIdx])
                            {
                                delTriIdx++;
                            }
                            else
                            {
                                newVert2[w++] = origVert2[i];
                                newVert2[w++] = origVert2[i + 1];
                                newVert2[w++] = origVert2[i + 2];
                            }
                        }
                        newD3dMesh.SetVertexBufferData(newVert2, LockFlags.None);
                        newD3dMesh.UnlockVertexBuffer();
                        break;
                    case TgcMesh.MeshRenderType.DIFFUSE_MAP_AND_LIGHTMAP:
                        newD3dMesh = new Mesh(triCount, vertCount, MeshFlags.Managed, TgcSceneLoader.DiffuseMapAndLightmapVertexElements, GuiController.Instance.D3dDevice);
                        TgcSceneLoader.DiffuseMapAndLightmapVertex[] origVert3 = (TgcSceneLoader.DiffuseMapAndLightmapVertex[])mesh.D3dMesh.LockVertexBuffer(
                            typeof(TgcSceneLoader.DiffuseMapAndLightmapVertex), LockFlags.ReadOnly, mesh.D3dMesh.NumberVertices);
                        mesh.D3dMesh.UnlockVertexBuffer();
                        TgcSceneLoader.DiffuseMapAndLightmapVertex[] newVert3 = (TgcSceneLoader.DiffuseMapAndLightmapVertex[])newD3dMesh.LockVertexBuffer(
                            typeof(TgcSceneLoader.DiffuseMapAndLightmapVertex), LockFlags.None, newD3dMesh.NumberVertices);
                        for (int i = 0; i < origVert3.Length; i += 3)
                        {
                            if (delTriIdx < deletedTriangles.Count && i == deletedTriangles[delTriIdx])
                            {
                                delTriIdx++;
                            }
                            else
                            {
                                newVert3[w++] = origVert3[i];
                                newVert3[w++] = origVert3[i + 1];
                                newVert3[w++] = origVert3[i + 2];
                            }
                        }
                        mesh.D3dMesh.UnlockVertexBuffer();
                        newD3dMesh.SetVertexBufferData(newVert3, LockFlags.None);
                        newD3dMesh.UnlockVertexBuffer();
                        break;
                }
                //Cambiar mesh
                mesh.changeD3dMesh(newD3dMesh);
                deletedTriangles.Clear();
            }
            
            //Aplicar movimiento de vertices
            for (int i = 0; i < vertices.Count; i++)
            {
                vertices[i].position += vertices[i].movement;
                vertices[i].movement = new Vector3(0, 0, 0);
            }

            //Actualizar vertexBuffer
            using (VertexBuffer vb = mesh.D3dMesh.VertexBuffer)
            {
                switch (mesh.RenderType)
                {
                    case TgcMesh.MeshRenderType.VERTEX_COLOR:
                        TgcSceneLoader.VertexColorVertex[] verts1 = (TgcSceneLoader.VertexColorVertex[])mesh.D3dMesh.LockVertexBuffer(
                            typeof(TgcSceneLoader.VertexColorVertex), LockFlags.None, mesh.D3dMesh.NumberVertices);
                        for (int i = 0; i < verts1.Length; i++)
                        {
                            verts1[i].Position = vertices[indexBuffer[i]].position;
                        }
                        mesh.D3dMesh.SetVertexBufferData(verts1, LockFlags.None);
                        mesh.D3dMesh.UnlockVertexBuffer();
                        break;
                    case TgcMesh.MeshRenderType.DIFFUSE_MAP:
                        TgcSceneLoader.DiffuseMapVertex[] verts2 = (TgcSceneLoader.DiffuseMapVertex[])mesh.D3dMesh.LockVertexBuffer(
                            typeof(TgcSceneLoader.DiffuseMapVertex), LockFlags.ReadOnly, mesh.D3dMesh.NumberVertices);
                        for (int i = 0; i < verts2.Length; i++)
                        {
                            verts2[i].Position = vertices[indexBuffer[i]].position;
                        }
                        mesh.D3dMesh.SetVertexBufferData(verts2, LockFlags.None);
                        mesh.D3dMesh.UnlockVertexBuffer();
                        break;
                    case TgcMesh.MeshRenderType.DIFFUSE_MAP_AND_LIGHTMAP:
                        TgcSceneLoader.DiffuseMapAndLightmapVertex[] verts3 = (TgcSceneLoader.DiffuseMapAndLightmapVertex[])mesh.D3dMesh.LockVertexBuffer(
                            typeof(TgcSceneLoader.DiffuseMapAndLightmapVertex), LockFlags.ReadOnly, mesh.D3dMesh.NumberVertices);
                        for (int i = 0; i < verts3.Length; i++)
                        {
                            verts3[i].Position = vertices[indexBuffer[i]].position;
                        }
                        mesh.D3dMesh.SetVertexBufferData(verts3, LockFlags.None);
                        mesh.D3dMesh.UnlockVertexBuffer();
                        break;
                }

            }

            //Actualizar indexBuffer (en forma secuencial)
            using (IndexBuffer ib = mesh.D3dMesh.IndexBuffer)
            {
                short[] seqIndexBuffer = new short[indexBuffer.Length];
                for (int i = 0; i < seqIndexBuffer.Length; i++)
                {
                    seqIndexBuffer[i] = (short)i;
                }
                ib.SetData(seqIndexBuffer, 0, LockFlags.None);
            }

            //Actualizar attributeBuffer
            int[] attributeBuffer = mesh.D3dMesh.LockAttributeBufferArray(LockFlags.None);
            foreach (EditPolyPolygon p in polygons)
            {
                //Setear en cada triangulo el material ID del poligono
                foreach (int idx in p.vbTriangles)
                {
                    int triIdx = idx / 3;
                    attributeBuffer[triIdx] = p.matId;
                }
            }
            mesh.D3dMesh.UnlockAttributeBuffer(attributeBuffer);

        }

        /// <summary>
        /// Actualizar estructuras internas en base a mesh original.
        /// Cuando se vuelve a entrar al modo EditablePoly luego de la primera vez.
        /// </summary>
        public void updateValuesFromMesh(TgcMesh mesh)
        {
            this.mesh = mesh;
            List<EditPolyVertex> origVertices = getMeshOriginalVertexData(mesh);
            for (int i = 0; i < origVertices.Count; i++)
            {
                EditPolyVertex origV = origVertices[i];
                EditPolyVertex v = vertices[indexBuffer[i]];
                v.position = origV.position;
                /*v.normal = origV.normal;
                v.color = origV.color;
                v.texCoords = origV.texCoords;
                v.texCoords2 = origV.texCoords2;*/
            }
            dirtyValues = true;
        }


        #endregion
















        
    }
}
