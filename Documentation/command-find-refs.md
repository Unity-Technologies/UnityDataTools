# find-refs Command (Experimental)

The `find-refs` command traces reference chains leading to specific objects. Use it to understand why an asset was included (and potentially duplicated) in a build.

> **Experimental**: `find-refs` does not yet produce complete or clearly-explained results for all supported build pipelines. For example, a Player build database never produces chains, and a "Found 0 reference chain(s)" result does not distinguish an explicitly-included asset from an unreferenced object. See [issue #121](https://github.com/Unity-Technologies/UnityDataTools/issues/121) for the known problems. Until they are addressed, prefer querying the analyze database's `refs_view` directly, as described in [Tracing why content is in the build](agent-guide.md#tracing-why-content-is-in-the-build).

It walks *up* the reference graph from the target object and **stops at the first asset it reaches**. The reported chains therefore end at the immediate containing asset, not at the ultimate root that transitively depends on the target. For example, if `RootAsset` references `LeafAsset` which references a texture, searching for the texture reports the chain ending at `LeafAsset`; to see that `RootAsset` pulls it in, search for `LeafAsset` instead.

## Quick Reference

```
UnityDataTool find-refs <database> [options]
```

| Option | Description | Default |
|--------|-------------|---------|
| `<database>` | Path to database from `analyze` command | *(required)* |
| `-i, --object-id <id>` | ID of object to analyze (from `id` column) | — |
| `-n, --object-name <name>` | Name of objects to analyze | — |
| `-t, --object-type <type>` | Type filter when using `-n` | — |
| `-o, --output-file <file>` | Output filename | `references.txt` |
| `--stdout` | Write the reference chains to stdout instead of a file | `false` |
| `-a, --find-all` | Find all chains instead of stopping at first | `false` |

> **Note:** Either `--object-id` or `--object-name` must be provided.
>
> `--stdout` and `-o/--output-file` are mutually exclusive.

## Prerequisites

This command requires a database created by the [`analyze`](command-analyze.md) command **without** the `--skip-references` option.

Reference chains end at an asset: an AssetBundle's assets, or — for ContentDirectory builds — the loadable objects described by the build's `ContentLayout.json`. For a ContentDirectory build, include the `ContentLayout.json` in the analyze input, both to resolve the references between content files and to provide those chain roots (see [ContentLayout in the Analyze Database](contentlayout-database.md)).

---

## Examples

Find references to an object by name and type, printing directly to the console:
```bash
UnityDataTool find-refs my_database.db -n "MyTexture" -t "Texture2D" --stdout
```

Write the reference chains to a file instead:
```bash
UnityDataTool find-refs my_database.db -n "MyTexture" -t "Texture2D" -o refs.txt
```

Find references to a specific object by ID:
```bash
UnityDataTool find-refs my_database.db -i 12345 --stdout
```

Find all duplicate references (useful for finding why an asset is duplicated):
```bash
UnityDataTool find-refs my_database.db -n "SharedMaterial" -t "Material" -a -o all_refs.txt
```

---

## Use Cases

| Scenario | Approach |
|----------|----------|
| **Why is this asset included?** | Use `-n` with the asset name |
| **Why is this asset duplicated?** | Use `-n` to find all instances (same name, different IDs) |
| **Trace specific object** | Use `-i` with the object ID from the database |
| **Find all reference chains** | Add `-a` flag (may take longer) |

---

## Output Format

The output shows the reference chain from root assets to your target object:

```
Reference chains to 
  ID:             1234
  Type:           Transform
  Archive:        archive_name
  SerializedFile: CAB-353837edf22eb1c4d651c39d27a233b7

Found reference in:
MyPrefab.prefab
(Archive = MyAssetBundle; SerializedFile = CAB-353837edf22eb1c4d651c39d27a233b7)
  GameObject (id=1348) MyPrefab
    ↓ m_Component.Array[0].component
    RectTransform (id=721) [Component of MyPrefab (id=1348)]
      ↓ m_Children.Array[9]
      RectTransform (id=1285) [Component of MyButton (id=1284)]
        ↓ m_GameObject
        GameObject (id=1284) MyButton
          ...
          Transform (id=1234) [Component of MyButtonEffectLayer (1) (id=938)]

Analyzed 266 object(s).
Found 1 reference chain(s).
```

### Reading the Output

| Element | Description |
|---------|-------------|
| `↓ property_name` | The property containing the reference |
| `[Component of X (id=Y)]` | Shows the GameObject for Components |
| `[Script = X]` | Shows the script name for MonoBehaviours |

**Refer to the [ReferenceFinder documentation](referencefinder.md) for more details.**

