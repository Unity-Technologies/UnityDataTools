# TextDumper

The TextDumper is a library providing an API that can be used to dump the content of a Unity data file (AssetBundle or SerializedFile) into human-readable text files.

## How to use

The API consists of a single class called TextDumper. It has a single method named Dump and taking three parameters:
* path (string): path of the data file.
* outputPath (string): path where the output files will be created.
* skipLargeArrays (bool): if true, the content of arrays larger than 1KB won't be dumped.

## How to interpret the output files

There will be one output file per SerializedFile. Depending on the type of the input file, there can be more than one output file (e.g. AssetBundles are archives that can contain several SerializedFiles).

The first lines of the output file will look like this:

    External References
    path(1): "Library/unity default resources" GUID: 0000000000000000e000000000000000 Type: 0
    path(2): "Resources/unity_builtin_extra" GUID: 0000000000000000f000000000000000 Type: 0
    path(3): "archive:/CAB-35fce856128a6714740898681ea54bbe/CAB-35fce856128a6714740898681ea54bbe" GUID: 00000000000000000000000000000000 Type: 0

This information can be used to dereference PPtrs. A PPtr is a type used by Unity to locate and load objects in SerializedFiles. It has two fields:
* m_FileID: the file identifier where the object is located
* m_PathID: the object identifier in the file
The file identifier is an index in the External References list above (the number in parenthesis). It will be 0 if the asset is in the same file.

The string after the path is the SerializedFile name corresponding to the file identifier in parenthesis. The GUID and Type are internal data used by Unity.

The rest of the file will contain an entry similar to this one for each object in the files:

    ID: -8138362113332287275 (ClassID: 135) SphereCollider 
      m_GameObject PPtr<GameObject> 
        m_FileID int 0
        m_PathID SInt64 -1473921323670530447
      m_Material PPtr<PhysicMaterial> 
        m_FileID int 0
        m_PathID SInt64 0
      m_IsTrigger bool False
      m_Enabled bool True
      m_Radius float 0.5
      m_Center Vector3f 
        x float 0
        y float 0
        z float 0

The first line contains the object identifier, the internal ClassID used by Unity, and the type name corresponding to this ClassID. Note that the object identifier is guaranteed to be unique in this file only.
The next lines are the serialized fields of the objects. The first value is the field name, the second is the type and the last is the value. If there is no value, it means that it is a sub-object that is dumped
on the next lines with a higher indentation level.
