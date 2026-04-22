using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PortalDatabaseJson
{
    public int version = 1;
    public List<PortalDefinition> portals = new List<PortalDefinition>();
}

[Serializable]
public class PortalDefinition
{
    public string id;
    public PortalDestination destination = new PortalDestination();
    public List<PortalMetadataEntry> metadata = new List<PortalMetadataEntry>();
}

[Serializable]
public class PortalDestination
{
    public string scene;
    public bool useSpawnPoint = true;
    public string spawnId;
    public bool usePortalExitOffset;
    public string destinationPortalId;
    public string exitSide = "Right";
    public float exitDistance = 1.25f;
    public SerializableVector3 position = new SerializableVector3();
    public SerializableVector3 rotationEuler = new SerializableVector3();
}

[Serializable]
public class PortalMetadataEntry
{
    public string key;
    public string value;
}

[Serializable]
public class SerializableVector3
{
    public float x;
    public float y;
    public float z;

    public SerializableVector3()
    {
    }

    public SerializableVector3(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public Vector3 ToVector3()
    {
        return new Vector3(x, y, z);
    }
}
