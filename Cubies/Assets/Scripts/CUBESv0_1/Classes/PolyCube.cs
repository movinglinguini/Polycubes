﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PolyCube
{
    /***********************************
    *   PUBLIC VARIABLES
    ************************************/

    /***********************************
    *   PRIVATE VARIABLES
    ************************************/
    private Dictionary<string, SingleFace> MapOfFaces = new Dictionary<string, SingleFace>();
    private Dictionary<string, SingleCube> MapOfCubes = new Dictionary<string, SingleCube>();
    private Dictionary<Transform, string> OriginalHingePos = new Dictionary<Transform, string>();
    private List<Transform> CutHeads;

    private AdjacencyMap DualGraph = new AdjacencyMap();
    private HingeMap EdgeGraph = new HingeMap();

    private Queue<SingleFace> FacesToParent = new Queue<SingleFace>();
    private Transform RotateEdge;
    private Queue<Transform> FacesToRotate = new Queue<Transform>();
    private Dictionary<Transform, Material> PaintedHinges = new Dictionary<Transform, Material>();
    private List<Transform> CutHinges = new List<Transform>();
    private List<Transform> CannotCut = new List<Transform>();
    private List<Transform> UnfoldingLine = new List<Transform>();
    private Transform FirstHingeCut = null;

    private float _CUBE_SCALE;
    private float _SPACING;
    private int cubeCount;
    private int faceCount;

    /************************************
    *   CONSTRUCTOR
    *************************************/
    public PolyCube(float cubeScale, float spacing)
    {
        Debug.Log(cubeScale);
        _CUBE_SCALE = cubeScale;
        _SPACING = spacing;
        cubeCount = 0;
        faceCount = 0;
        CutHeads = new List<Transform>();
    }

    
    //Create a new object that acts as a super-hinge that its child faces will rotate around?
    public void SetRotationEdge(Transform edge, bool replaceHinge)
    {
        if(!replaceHinge)
           RotateEdge = edge;
        else
        {
            FacesToRotate.Clear();
            RotateEdge = edge;
        }
    }

    public void ReparentFace(Transform face)
    {
        if(!FacesToRotate.Contains(face))
            FacesToRotate.Enqueue(face.parent);
    }

    public void StartRotate()
    {
        int amt_faces = FacesToRotate.Count;

        for(int i = 0; i < amt_faces; i++)
        {
            FacesToRotate.Dequeue().RotateAround(RotateEdge.position, RotateEdge.forward, 45.0f);
        }
    }

    //Adds a new cube to the polycube structure
    public void AddCube(Vector3 position, GameObject cube)
    {
        if(position != null && cube != null)
        {

            Clean(cube);

            //Some garbage collection here. In the case that we have an empty cube object, we destroy it.
            //We also bar any attempt at adding its faces (which no longer exist) to the adjacency graph.
            if (cube.transform.childCount > 0)
            {
                cubeCount += 1;

                SingleCube c = new SingleCube(position, cube.transform);

                while(FacesToParent.Count > 0)
                {
                    SingleFace f = FacesToParent.Dequeue();
                    c.AddNewFace(f, ref OriginalHingePos);
                    f.Parent = c;
                }

                MapOfCubes.Add(PreciseVector.Vector3ToDecimalString(position, 1), c);
                cube.transform.position = cube.transform.position * _SPACING;
                cube.SetActive(true);
            }
            else
            {
                cubeCount -= 1;
                Object.Destroy(cube);
            }
        }
    }

    public void Repaint(Material DefaultMaterial, Material CutMaterial, Material InvalidCutMaterial, Material UnfoldMaterial)
    {
        foreach(Transform C in OriginalHingePos.Keys)
        {
            C.GetComponent<Renderer>().material = DefaultMaterial;
        }

        foreach(Transform C in CutHinges)
        {
            C.GetComponent<Renderer>().material = CutMaterial;
        }

        foreach(Transform C in CannotCut)
        {
            C.GetComponent<Renderer>().material = InvalidCutMaterial;
        }

        foreach(Transform C in UnfoldingLine)
        {
            C.GetComponent<Renderer>().material = UnfoldMaterial;
        }
    }

    public void BuildDualGraph()
    {
       foreach(SingleFace face in MapOfFaces.Values)
       {
           FindAdjacentFaces(face);
       }

        EdgeGraph.SetEdgeRelationships();
    }

    //Input: Transform of hinge, a color for labeling cut edges, a color for edges that cannot be cut because cutting them would disconnect the dual graph
    //Description: Manipulate the dual graph so that nodes in the graph are disconnected (i.e. edges are removed from the graph) according to the hinge the user
    //              selected for cutting. Also, find the edges on the polycube that, when cut, would cause the graph to be disconnected and label them as "invalid"
    public void CutPolyCube(Transform NewCut, Material CutColor, Material InvalidColor, Material DefaultColor, Material UnFold)
    {

        if (!EdgeGraph.IsCut(NewCut))
        {
            if (EdgeGraph.IsAdjacentToCut(NewCut) || CutHinges.Count == 0)
            {
                List<Transform> ValidCuts;
                bool valid = DualGraph.CutIfAllowed(NewCut, EdgeGraph, out ValidCuts);

                if (valid)
                {
                    if (CutHinges.Count == 0)
                    {
                        FirstHingeCut = ValidCuts[0];
                    }

                    CutHinges.Add(ValidCuts[0]);
                    CutHinges.Add(ValidCuts[1]);
                }
                else
                {
                    Debug.Log("Cannot cut there");
                }
            }      
        }
        else
        {
            List<Transform> HingePair;
            DualGraph.ReconnectAroundEdge(NewCut, EdgeGraph, out HingePair);


            CutHinges.Remove(HingePair[0]);
            CutHinges.Remove(HingePair[1]);
        }

        List<Transform>InvalidCuts = DualGraph.FindInvalidEdges(EdgeGraph);
        CannotCut = InvalidCuts;

        List<Transform> Line;
        if (EdgeGraph.CheckIfUnfoldPossible(DualGraph, out Line))
        {
            Debug.Log("Unfolding possible");
        }
        UnfoldingLine = Line;
    }

    public int GetCubeCount()
    {
        return cubeCount;
    }

    public void DumpAdjacency()
    {
        Debug.Log("-------DUAL GRAPH-------");
        DualGraph.DataDump();
        Debug.Log("--------END DUAL GRAPH------");
    }

    public void DumpMapOfCubes()
    {
        Debug.Log("-----MAP OF CUBES----");
        foreach(string key in MapOfCubes.Keys)
        {
            Debug.Log("-----CUBE POSITION----");
            Debug.Log(key);
            MapOfCubes[key].DumpFaces();
        }
        Debug.Log("-------END MAP OF CUBES---------");
    }

    public void DumpMapOfFaces()
    {
        Debug.Log("---------MAP OF FACES-------");
        foreach(string key in MapOfFaces.Keys)
        {
            Debug.Log(MapOfFaces[key].Trans.name + " : " + key);
        }
        Debug.Log("--------END MAP OF FACES-------");
    }

    public void DumpMapOfEdges()
    {
        EdgeGraph.EdgeDump();
    }

    public List<Transform> GetAdjacentHinges(Transform trans)
    {
        return EdgeGraph.GetAdjacentEdges(trans);
    }

    //Retrieve Collinear, Perpendicular, or Parallel edges    
    public List<Transform> GetRelatedHinges(Transform trans, string RelationshipType)
    {
        List<Transform> ListHinges;

        if (string.Compare(RelationshipType, "Collinear") == 0)
        {
            ListHinges = EdgeGraph.GetCollinearEdges(trans);
        }
        else if (string.Compare(RelationshipType, "Parallel") == 0)
        {
            ListHinges = EdgeGraph.GetParallelEdges(trans);
        }
        else if (string.Compare(RelationshipType, "Perpendicular") == 0)
        {
            ListHinges = EdgeGraph.GetPerpendicularEdges(trans);
        }
        else
            ListHinges = new List<Transform>();

        return ListHinges;
    }

    /*
    public List<Transform> GetPerpendicularHinges(Transform trans)
    { }*/

    public void DoublePaintHinge(Transform hinge, Material NewColor)
    {
        List<Transform> Hinges = EdgeGraph.GetEdgeFromHinge(hinge);

        PaintHinge(Hinges[0], NewColor);
        PaintHinge(Hinges[1], NewColor);
    }

    public void PaintHinge(Transform hinge, Material NewColor)
    {
        hinge.GetComponent<Renderer>().material = NewColor;
    }

    //Removes incident faces
    private void Clean(GameObject cube)
    {

        Queue<GameObject> FacesToDestroy = new Queue<GameObject>(); //A convenient queue for faces that are incident to faces already in the polycube
        //For each face in the cube...
        foreach (Transform face in cube.transform)
        {
            //string pos = PreciseVector.Vector3ToDecimalString(face.position, 1);
            //Check if that face would already exist in a downscaling of the polycube
            if (!MapOfFaces.ContainsKey(PreciseVector.Vector3ToDecimalString(face.position, 1)))
            {  
                //if it does not, then we can safely add it to the MapOfFaces
                face.name = "face_" + (++faceCount);
                SingleFace f = new SingleFace(face.position - cube.transform.position, face);
                FacesToParent.Enqueue(f);
                MapOfFaces.Add(PreciseVector.Vector3ToDecimalString(f.GetLatticePosition(), 1), f);
            }
            else
            {
                //If it does, then we need to destroy it.
                //We cannot destroy the child face of the cube we are currently checking immediately
                //as that will break the loop and cause a crash. Instead we...
                //...remove the face from the polycube from the MapOfFaces...
                string PositionofIncidence;
                SingleFace faceToDestroy;
                MapOfFaces.TryGetValue(PreciseVector.Vector3ToDecimalString(face.position, 1), out faceToDestroy);

                PositionofIncidence = PreciseVector.Vector3ToDecimalString(face.position, 1);

                SingleCube c;
                c = MapOfFaces[PositionofIncidence].Parent;
                c.RemoveFace(MapOfFaces[PositionofIncidence]);

                //Remove the cube c if it has no faces left.
                if (c.ListOfFaces.Count == 0)
                {
                    MapOfCubes.Remove(PreciseVector.Vector3ToDecimalString(c.Cube.position, 1));
                    Object.Destroy(c.Cube.gameObject);
                    cubeCount -= 1;
                }

                MapOfFaces.Remove(PositionofIncidence);
                
                //Remove all hinges that belong to this face from the map of hinges
                foreach(Transform edge in faceToDestroy.Trans)
                {
                    OriginalHingePos.Remove(edge);
                }

                Object.Destroy(faceToDestroy.Trans.gameObject); //...then we destroy that face in the polycube
                FacesToDestroy.Enqueue(face.gameObject); //then we queue the face from the cube we are checking for destruction.
            }
        }
        
        //Destroy all faces of the cube that we are checking that were incident with faces in the polycube.
        while(FacesToDestroy.Count > 0)
        {
            Object.Destroy(FacesToDestroy.Dequeue());
        }
    }

    //Adds faces to adjacency list by searching for faces that have been added to the polycube.
    //We search for the faces by taking advantage of the fact that the edge object's normal is the same as the face's normal.
    //We only need to search up or down along the normal by 0.5 * scale units from the edge's center to find another face.

    //TODO: Fix this up and add robustness to it.
    private void FindAdjacentFaces(SingleFace face)
    {
        //Debug.Log("Check for " + face.Trans.name + "'s adjacency");

        Vector3 normal = face.Trans.up;
        SingleCube Parent = face.Parent;
        List<Vector3> ParentDirections = new List<Vector3>();
        //Debug.Log("Parent cube's position: " + Parent.Position);

        ParentDirections.Add(Parent.Cube.up);
        ParentDirections.Add(Parent.Cube.right);
        ParentDirections.Add(-Parent.Cube.up);
        ParentDirections.Add(-Parent.Cube.right);
        ParentDirections.Add(Parent.Cube.forward);
        ParentDirections.Add(-Parent.Cube.forward);


        /*Check for adjacent faces within the cube*/
        foreach (SingleFace f in Parent.ListOfFaces)
        {
            if (normal + f.Trans.up != Vector3.zero && face != f)
            {
                DualGraph.AddNeighbors(face.Trans, f.Trans);
                //Debug.Log("Adjacent faces: " + f.Trans.name + " and " + face.Trans.name);

                FindAdjacentEdges(face.Trans, f.Trans);
            }
        }

        foreach(Vector3 dir in ParentDirections)
        {
            if(dir + normal != Vector3.zero)
            {
                //Debug.Log("Checking neighbor of parent cube at " + (Parent.Position + dir));
                if (MapOfCubes.ContainsKey(PreciseVector.Vector3ToDecimalString(Parent.Position + dir, 1)))
                {
                    CheckNeighborCubes(face, MapOfCubes[PreciseVector.Vector3ToDecimalString(Parent.Position + dir, 1)]);
                }
            }
        }
    }

    /*
    Input: The face that we are currently checking for adjacency
    Description: Check the neighboring cube's faces to see if they have the same normal as current_face. If so, 
    then it is an adjacent face. Afterwards, check a neighbor of the neighbor cube for adjacent faces (see CheckDiagonal).
    */
    private void CheckNeighborCubes(SingleFace current_face, SingleCube neighbor)
    {
        //Debug.Log("Checking neighbor...");
        foreach(SingleFace f in neighbor.ListOfFaces)
        {
            if(current_face.Trans.up == f.Trans.up)
            {
                //Debug.Log("Adjacent faces: " + f.Trans.name + " and " + current_face.Trans.name);
                DualGraph.AddNeighbors(current_face.Trans, f.Trans);

                FindAdjacentEdges(current_face.Trans, f.Trans);
            }
        }
        //Debug.Log("Check for neighbor's neighbor at " + (neighbor.Position + current_face.Trans.up));
        if (MapOfCubes.ContainsKey(PreciseVector.Vector3ToDecimalString(neighbor.Position + current_face.Trans.up, 1)))
            CheckDiagonal(current_face, MapOfCubes[PreciseVector.Vector3ToDecimalString(neighbor.Position + current_face.Trans.up, 1)]);
    }

    /*
    Input: The face that we are currently checking for adjacency
    Description: Check the neighbor to the face's parent cube's neighbor, which is forward on the parent cube's plane, for adjacent faces.
    Two faces are adjacent in this case if the normals of each face intersect.
    */
    private void CheckDiagonal(SingleFace current_face, SingleCube n_neighbor)
    {
        //Debug.Log("Checking neighbor of neighbor");
        foreach (SingleFace f in n_neighbor.ListOfFaces)
        {
            if (current_face.GetLatticePosition() + current_face.Trans.up*0.5f == f.GetLatticePosition() + f.Trans.up*0.5f)
            {
                //Debug.Log("Adjacent faces: " + f.Trans.name + " and " + current_face.Trans.name);
                DualGraph.AddNeighbors(current_face.Trans, f.Trans);

                FindAdjacentEdges(current_face.Trans, f.Trans);
            }
        }
    }

    private void FindAdjacentEdges(Transform f_1, Transform f_2)
    {
        foreach (Transform hinge in f_1)
        {
            if (hinge.name == "edge")
            {
                foreach (Transform hinge_2 in f_2)
                {
                    if (hinge_2.name == "edge")
                    {
                        if (OriginalHingePos[hinge] == OriginalHingePos[hinge_2])
                            EdgeGraph.SetEdge(hinge, hinge_2, PreciseVector.StringToVector3(OriginalHingePos[hinge]));
                    }
                }
            }
        }
    }

    //Returns normal of face on the polycube
    private Vector3 _GetFaceNormal(Transform face)
    {
        return face.up;
    }
}
