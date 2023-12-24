using Oculus.Interaction;
using System.Collections.Generic;
using UnityEngine;

public class Pen : MonoBehaviour
{
    [Header("Pen Properties")]
    public Transform tip;
    public Material drawingMaterial;
    public Material tipMaterial;
    [Range(0.005f, 0.1f)]
    public float penWidth = 0.005f;
    public Color[] penColors;

    private List<LineRenderer> lineRenderers = new List<LineRenderer>(); // List to store line renderers

    [Header("Hands & Grabbable")]
    public OVRHand rightHand;
    public OVRHand leftHand;
    public Grabbable grabbable;

    private LineRenderer currentDrawing;
    private List<Vector3> positions = new();
    private int index;
    private int currentColorIndex;
    private bool wasLeftIndexPinching;
    private Vector3 lastPosition;

    private void Start()
    {
        currentColorIndex = 0;
        tipMaterial.color = penColors[currentColorIndex];
    }

    private void Update()
    {
        if (leftHand.GetFingerPinchStrength(OVRHand.HandFinger.Index) > 0.95f)
        {
            Draw();
        }
        else if (currentDrawing != null)
        {
            currentDrawing = null;
        }

        // Check for left hand index finger pinch start
        bool isLeftIndexPinching = leftHand.GetFingerIsPinching(OVRHand.HandFinger.Middle);
        if (!wasLeftIndexPinching && isLeftIndexPinching)
        {
            SwitchColor();
        }
        wasLeftIndexPinching = isLeftIndexPinching;

        if (isShapeClosed())
        {
            ShapeType shapeType = RecognizeShape();
            ConvertToShape(shapeType);
        }

        if (IsHandMakingFist(rightHand) || IsHandMakingFist(leftHand))
        {
            DeleteAllLines();
        }
    }

    private void Draw()
    {
        if (currentDrawing == null)
        {
            index = 0;
            currentDrawing = CreateNewLineRenderer(); // Create a new LineRenderer for each line segment
            lineRenderers.Add(currentDrawing); // Add the new LineRenderer to the list
            lastPosition = tip.transform.position;
        }
        else if (Vector3.Distance(lastPosition, tip.transform.position) > 0.01f)
        {
            index++;
            currentDrawing.positionCount = index + 1;
            currentDrawing.SetPosition(index, tip.transform.position);
            lastPosition = tip.transform.position;
        }
    }

    private LineRenderer CreateNewLineRenderer()
    {
        GameObject lineObj = new GameObject("LineSegment");
        LineRenderer newLineRenderer = lineObj.AddComponent<LineRenderer>();
        newLineRenderer.material = new Material(drawingMaterial);
        newLineRenderer.startColor = newLineRenderer.endColor = penColors[currentColorIndex];
        newLineRenderer.startWidth = newLineRenderer.endWidth = penWidth;
        newLineRenderer.positionCount = 1;
        newLineRenderer.SetPosition(0, tip.transform.position);
        return newLineRenderer;
    }

    private bool isShapeClosed()
    {
        if (positions.Count > 10) // Arbitrary number, adjust based on your needs
        {
            float distance = Vector3.Distance(positions[0], positions[positions.Count - 1]);
            return distance < 0.05f; // Threshold for considering the shape closed
        }
        return false;
    }

    private void SwitchColor()
    {
        if(currentColorIndex == penColors.Length - 1)
        {
            currentColorIndex = 0;
        } else
        {
            currentColorIndex++;
        }

        tipMaterial.color = penColors[currentColorIndex];
    }

    private ShapeType RecognizeShape()
    {
        int pointCount = positions.Count;
        if (pointCount < 3) return ShapeType.Undefined;

        // For simplicity, let's assume:
        // - A triangle will have exactly 3 significant points.
        // - A rectangle will have 4.
        // - A circle will have more and will be equidistant from a central point.
        if (pointCount == 3)
        {
            return ShapeType.Triangle;
        }
        else if (pointCount == 4)
        {
            return ShapeType.Rectangle;
        }
        else
        {
            // Check if all points are roughly equidistant from the center
            Vector3 center = Vector3.zero;
            foreach (var point in positions)
            {
                center += point;
            }
            center /= pointCount;

            float averageDistance = 0f;
            foreach (var point in positions)
            {
                averageDistance += Vector3.Distance(center, point);
            }
            averageDistance /= pointCount;

            bool isCircle = true;
            foreach (var point in positions)
            {
                if (Mathf.Abs(Vector3.Distance(center, point) - averageDistance) > 0.05f) // Threshold
                {
                    isCircle = false;
                    break;
                }
            }

            if (isCircle)
            {
                return ShapeType.Circle;
            }
        }

        return ShapeType.Undefined;
    }


    private void ConvertToShape(ShapeType shapeType)
    {
        switch (shapeType)
        {
            case ShapeType.Circle:
                DrawCircle();
                break;
            case ShapeType.Rectangle:
                DrawRectangle();
                break;
            case ShapeType.Triangle:
                DrawTriangle();
                break;
        }
    }

    private void DrawCircle()
    {
        Vector3 center = CalculateCenter(positions);
        float radius = CalculateAverageRadius(positions, center);
        int segments = 20; // You can increase this for a smoother circle

        LineRenderer lineRenderer = CreateLineRenderer();
        lineRenderer.positionCount = segments + 1;

        for (int i = 0; i <= segments; i++)
        {
            float angle = 2 * Mathf.PI * i / segments;
            lineRenderer.SetPosition(i, new Vector3(center.x + Mathf.Cos(angle) * radius, center.y + Mathf.Sin(angle) * radius, center.z));
        }
    }

    private void DrawRectangle()
    {
        List<Vector3> corners = CalculateRectangleCorners(positions);
        if (corners.Count != 4) return; // Error handling

        LineRenderer lineRenderer = CreateLineRenderer();
        lineRenderer.positionCount = 5;

        for (int i = 0; i < 4; i++)
        {
            lineRenderer.SetPosition(i, corners[i]);
        }
        lineRenderer.SetPosition(4, corners[0]); // Close the rectangle
    }


    private void DrawTriangle()
    {
        if (positions.Count < 3) return; // Error handling

        LineRenderer lineRenderer = CreateLineRenderer();
        lineRenderer.positionCount = 4;

        for (int i = 0; i < 3; i++)
        {
            lineRenderer.SetPosition(i, positions[i]);
        }
        lineRenderer.SetPosition(3, positions[0]); // Close the triangle
    }

    private LineRenderer CreateLineRenderer()
    {
        GameObject lineObj = new GameObject("Shape");
        LineRenderer lineRenderer = lineObj.AddComponent<LineRenderer>();
        lineRenderer.material = drawingMaterial;
        lineRenderer.startWidth = lineRenderer.endWidth = penWidth;
        return lineRenderer;
    }

    private Vector3 CalculateCenter(List<Vector3> points)
    {
        Vector3 sum = Vector3.zero;
        foreach (Vector3 point in points)
        {
            sum += point;
        }
        return sum / points.Count;
    }

    private float CalculateAverageRadius(List<Vector3> points, Vector3 center)
    {
        float sumDistance = 0f;
        foreach (Vector3 point in points)
        {
            sumDistance += Vector3.Distance(center, point);
        }
        return sumDistance / points.Count;
    }
    private List<Vector3> CalculateRectangleCorners(List<Vector3> points)
    {
        if (points.Count < 4) return null;

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        foreach (Vector3 point in points)
        {
            if (point.x < minX) minX = point.x;
            if (point.x > maxX) maxX = point.x;
            if (point.y < minY) minY = point.y;
            if (point.y > maxY) maxY = point.y;
            if (point.z < minZ) minZ = point.z;
            if (point.z > maxZ) maxZ = point.z;
        }

        List<Vector3> corners = new List<Vector3>
    {
        new Vector3(minX, minY, minZ),
        new Vector3(maxX, minY, minZ),
        new Vector3(maxX, maxY, minZ),
        new Vector3(minX, maxY, minZ)
    };

        return corners;
    }

    enum ShapeType { Circle, Rectangle, Triangle, Undefined }


    private bool IsHandMakingFist(OVRHand hand)
    {
        // Adjust the threshold as needed
        float fistThreshold = 0.9f;
        return hand.GetFingerPinchStrength(OVRHand.HandFinger.Index) > fistThreshold &&
               hand.GetFingerPinchStrength(OVRHand.HandFinger.Middle) > fistThreshold &&
               hand.GetFingerPinchStrength(OVRHand.HandFinger.Ring) > fistThreshold &&
               hand.GetFingerPinchStrength(OVRHand.HandFinger.Pinky) > fistThreshold;
    }

    private void DeleteAllLines()
    {
        foreach (var lineRenderer in lineRenderers)
        {
            Destroy(lineRenderer.gameObject);
        }
        lineRenderers.Clear();
        currentDrawing = null;
    }
}
