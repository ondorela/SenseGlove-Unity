﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A class that can hook itself up to a SenseGlove_Interactable or material, and deform its mesh.
/// </summary>
[RequireComponent(typeof(SenseGlove_Material))]
public class SenseGlove_MeshDeform : MonoBehaviour
{
    //----------------------------------------------------------------------------------------------
    // Properties

    #region Properties

    /// <summary> Will be used to extract the Mesh variable without exposing it to other classes. </summary>
    [Tooltip("The filter used to extract the mesh of the object to deform.")]
    public MeshFilter meshFilter;

    /// <summary> Determines how the Vertices respond to the collider(s) </summary>
    [Tooltip("Determines how the Vertices respond to the collider(s)")]
    public DisplaceType displaceType = DisplaceType.Plane;

    /// <summary> The Maximum that a vertex can displace from its original position </summary>
    [Tooltip("The Maximum that a vertex can displace from its original position")]
    public float maxDisplacement = 0.01f;



    /// <summary> The actual Mesh to manipulate. </summary>
    private Mesh myMesh;

    /// <summary> The original vertices of the mesh, used for Deformation Logic </summary>
    private Vector3[] verts;

    /// <summary> The deformed mesh vertices, which are used to update the Mesh </summary>
    private Vector3[] deformVerts;

    /// <summary> Indicated that the Mesh should be defroming. No need to recalculate unless they are being touched by a Feedback Collider. </summary>
    private bool atRest = true;

    /// <summary> The indices (in myMesh.vertices) that represent points that may be shared with others. </summary>
    private int[] uniqueVertices;

    /// <summary> The points shared by the Vertices at each indes of uniqueVertices. </summary>
    private int[][] sameVertices;

    /// <summary> The queue of deformations that will be aplied during the next update frame. </summary>
    private List<Deformation> deformationQueue = new List<Deformation>();

    #endregion Properties

    //----------------------------------------------------------------------------------------------
    // Monobehaviour

    #region Monobehaviour

    // Use this for initialization
    void Start ()
    {
        this.CollectMeshData();
    }
	
	// Update is called once per frame
	void Update ()
    {
		
	}

    //Called at a fixed rate, same time as the physics engine.
    void FixedUpdate()
    {
        this.UpdateMesh(); //can be moved to Update to call the mesh deformation at different speeds.
    }

    //Called when the script is disabled.
    void OnDisable()
    {
        this.ResetMesh();
    }


    #endregion Monobehaviour

    //----------------------------------------------------------------------------------------------
    // Mesh Deformation

    #region MeshDeformation

    /// <summary> Collect the Mesh Data and find its unique vertices. </summary>
    /// <remarks>Placed in a separate function so one can re-analyze the mesh data on the fly.</remarks>
    void CollectMeshData()
    {
        if (this.meshFilter == null)
        {
            this.meshFilter = this.GetComponent<MeshFilter>();
        }

        if (this.meshFilter != null)
        {
            this.myMesh = this.meshFilter.mesh;
            if (myMesh != null)
            {
                this.verts = myMesh.vertices;
                this.deformVerts = myMesh.vertices;

                List<int>[] samePoints = new List<int>[verts.Length];
                //List<int> distinctPoints = new List<int>();

                int uniquePoints = 0;

                for (int i = 0; i < this.verts.Length; i++)
                {
                    this.deformVerts[i] = this.verts[i];
                    samePoints[i] = new List<int>();
                    for (int j = 0; j < this.verts.Length; j++)
                    {
                        if (i != j && verts[i].Equals(verts[j]))
                        {
                            //SenseGlove_Debugger.Log("Vertex " + i + " is the same as Vertex " + j);
                            samePoints[i].Add(j);
                        }
                    }

                    bool alreadyCounted = false;
                    for (int s = 0; s < samePoints[i].Count; s++)
                    {
                        if (samePoints[i][s] < i) //if one of the same vertice index is smaller, we have already counted it. 
                        {
                            alreadyCounted = true;
                        }
                    }
                    if (!alreadyCounted)
                    {
                        uniquePoints++;
                    }

                }

                this.uniqueVertices = new int[uniquePoints];
                this.sameVertices = new int[uniquePoints][];

                int n = 0;
                for (int i = 0; i < this.verts.Length; i++)
                {
                    bool alreadyCounted = false;
                    for (int s = 0; s < samePoints[i].Count; s++)
                    {
                        if (samePoints[i][s] < i) //if one of the same vertice index is smaller, we have already counted it. 
                        {
                            alreadyCounted = true;
                        }
                    }
                    if (!alreadyCounted)
                    {
                        this.uniqueVertices[n] = i;
                        this.sameVertices[n] = samePoints[i].ToArray();
                        n++;
                    }
                }

                //SenseGlove_Debugger.Log("Found a mesh with " + this.verts.Length + " vertices; " + uniquePoints + " of which are unique, and " + this.myMesh.triangles.Length / 3 + " triangles.");
            }
        }
    }


    /// <summary> Check if one Vertex equals another </summary>
    /// <param name="v1"></param>
    /// <param name="v2"></param>
    /// <returns></returns>
    public bool SameVertex(Vector3 v1, Vector3 v2)
    {
        return v1.x == v2.x && v1.y == v2.y && v1.z == v2.z;
    }

    /// <summary> Add a deformation to calculate at the end of the fixedUpdate function. </summary>
    /// /// <param name="absEntryVector"></param>
    /// <param name="absDeformPoint"></param>
    public void AddDeformation(Vector3 absEntryVector, Vector3 absDeformPoint, float dist)
    {
        Vector3 N = this.transform.InverseTransformDirection(absEntryVector);

        for (int i = 0; i < this.deformationQueue.Count;)
        {
            if (N.Equals(this.transform.InverseTransformDirection(deformationQueue[i].absEntryVector)))
            {
                if (dist < this.deformationQueue[i].distance)
                {
                    return;
                }
                else
                {
                    RemoveDeform(i);
                }
            }
            else
            {
                i++;
            }
        }

        //if we're here, the distance is greater than the current version

        // this.ClearDeformations();

        this.AddDeform(absEntryVector, absDeformPoint, dist);

        this.atRest = this.deformationQueue.Count <= 0; //only if no new deformations were added is this mesh at rest.
    }


    /// <summary> Add a single Deformation to the queue </summary>
    /// <param name="absEntryVector"></param>
    /// <param name="absDeformPoint"></param>
    /// <param name="dist"></param>
    private void AddDeform(Vector3 absEntryVector, Vector3 absDeformPoint, float dist)
    {
        //ensure that the deformPoint is not max dist away from the entryvector (?)
        this.deformationQueue.Add(new Deformation(absEntryVector, absDeformPoint, dist));
    }

    /// <summary> Remove a deformation from the queue </summary>
    /// <param name="index"></param>
    private void RemoveDeform(int index)
    {
        if (index >= 0 && index < this.deformationQueue.Count)
        {
            this.deformationQueue.RemoveAt(index);
        }
    }

    /// <summary> Clear the list of deforms after everything;s been applied. </summary>
    private void ClearDeformations()
    {
        this.deformationQueue.Clear();
    }

    /// <summary> Reset all (unique) vertices. </summary>
    /// <param name="resetAll">Set to true to reset all points, set to false to reset only the uniqueVertices (saves time)</param>
    private void ResetPoints(bool resetAll)
    {
        if (resetAll)
        {
            for (int i = 0; i < this.deformVerts.Length; i++)
            {
                this.deformVerts[i] = this.verts[i];
            }
        }
        else //reset unique vertices only
        {
            for (int i = 0; i < this.uniqueVertices.Length; i++)
            {
                Vector3 originalPoint = this.verts[this.uniqueVertices[i]];
                this.deformVerts[this.uniqueVertices[i]] = originalPoint;
            }
        }
    }


    /// <summary> Actually deform the mesh </summary>
    /// <param name="absEntryVector"></param>
    /// <param name="absDeformPoint"></param>
    public void DeformMesh(Vector3 absEntryVector, Vector3 absDeformPoint)
    {
        if (displaceType == DisplaceType.Plane)
        {
            Vector3 localNormal = this.transform.InverseTransformDirection(absEntryVector.normalized);
            Vector3 localPoint = this.transform.InverseTransformPoint(absDeformPoint);

            // SenseGlove_Debugger.Log("Checking the deform at " + SenseGlove_Util.ToString(localPoint) + " in the direction of " + SenseGlove_Util.ToString(localNormal));

            int def = 0; //debug variable
            int max = 0; //debug variable

            for (int i = 0; i < this.uniqueVertices.Length; i++)
            {
                Vector3 vert = this.deformVerts[this.uniqueVertices[i]];
                Vector3 V = (vert - localPoint);
                float dot = Vector3.Dot(localNormal, V);
                bool abovePlane = dot > 0;

                if (abovePlane)
                {   //its above the normal D:

                    //Project the Vector onto the plane with normal and point.
                    Vector3 d = Vector3.Project(V, localNormal);
                    Vector3 projectedPoint = vert - d;

                    if (this.transform.TransformVector(projectedPoint - this.verts[this.uniqueVertices[i]]).magnitude > this.maxDisplacement) //limit to max displacement
                    {
                        max++;
                        projectedPoint = vert - this.transform.InverseTransformVector(absEntryVector.normalized * this.maxDisplacement);
                    }

                    this.UpdatePoint(i, projectedPoint);
                    def++;
                    def += this.sameVertices[i].Length;
                }
                else
                {
                    //TODO: It's no longer being pushed, so move back
                }
            }
            //SenseGlove_Debugger.Log("Deformed " + def + " vertices, " + max + " of which have reaced maximum displacement,");
            this.atRest = false;
        }
        
    }

    /// <summary> Update a vertex in the uniqueVertices array, and its associated sameVertices. </summary>
    /// <param name="i"></param>
    /// <param name="newPos"></param>
    private void UpdatePoint(int uniqueVertIndex, Vector3 newPos)
    {
        this.deformVerts[this.uniqueVertices[uniqueVertIndex]] = newPos;
        for (int i = 0; i < this.sameVertices[uniqueVertIndex].Length; i++)
        {
            this.deformVerts[this.sameVertices[uniqueVertIndex][i]] = newPos;
        }
    }




    /// <summary> Apply all deformation in the Queue </summary>
    private void UpdateMesh()
    {
        if (this.myMesh && !this.atRest)
        {
            this.ResetPoints(false); //reset only the unique vertices

            //   SenseGlove_Debugger.Log("Applying " + this.vectors.Count + " deformations");
            for (int i = 0; i < this.deformationQueue.Count; i++)
            {
                this.DeformMesh(this.deformationQueue[i].absEntryVector, this.deformationQueue[i].absDeformPosition);
            }
            this.ClearDeformations(); //empties the deformation queue only.

            //SenseGlove_Debugger.Log("UpdateMesh()");
            myMesh.vertices = deformVerts;
            myMesh.RecalculateBounds();
            myMesh.RecalculateNormals();
        }
    }

    /// <summary> Reset the points in the mesh to their original vertices. </summary>
    public void ResetMesh()
    {
        //SenseGlove_Debugger.Log("ResetMesh()");
        if (myMesh != null)
        {
            this.ResetPoints(true);

            myMesh.vertices = deformVerts;
            myMesh.RecalculateBounds();
            myMesh.RecalculateNormals();
        }
        this.atRest = true;
    }

#endregion MeshDeformation

}

/// <summary> The method by which the mesh will be displaced. </summary>
public enum DisplaceType
{
    Plane = 0
}

/// <summary> Contains all variables needed to perform Deformations, and to evaluate two deformations. </summary>
public struct Deformation
{
    /// <summary> The absolute entry vector of the Deformation </summary>
    public Vector3 absEntryVector;

    /// <summary> The (current) absulute position of the deformation. </summary>
    public Vector3 absDeformPosition;

    /// <summary> How far the abdDeformPosition is from the  </summary>
    public float distance;

    /// <summary> Create a new Deformation data package. </summary>
    /// <param name="absEntryVect"></param>
    /// <param name="absPosition"></param>
    /// <param name="dist"></param>
    public Deformation(Vector3 absEntryVect, Vector3 absDefPosition, float dist)
    {
        this.absEntryVector = absEntryVect;
        this.absDeformPosition = absDefPosition;
        this.distance = dist;
    }

}