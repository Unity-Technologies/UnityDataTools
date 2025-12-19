# find-refs Command

> ⚠️ **Experimental:** This command may not work as expected in all cases.

The `find-refs` command traces reference chains leading to specific objects. Use it to understand why an asset was included (and potentially duplicated) in a build.

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
| `-o, --output-file <file>` | Output filename | — |
| `-a, --find-all` | Find all chains instead of stopping at first | `false` |

> **Note:** Either `--object-id` or `--object-name` must be provided.

## Prerequisites

This command requires a database created by the [`analyze`](analyze.md) command **without** the `--skip-references` option.

---

## Examples

Find references to an object by name and type:
```bash
UnityDataTool find-refs my_database.db -n "MyTexture" -t "Texture2D" -o refs.txt
```

Find references to a specific object by ID:
```bash
UnityDataTool find-refs my_database.db -i 12345 -o refs.txt
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
  AssetBundle:    asset_bundle_name
  SerializedFile: CAB-353837edf22eb1c4d651c39d27a233b7

Found reference in:
MyPrefab.prefab
(AssetBundle = MyAssetBundle; SerializedFile = CAB-353837edf22eb1c4d651c39d27a233b7)
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

**Refer to the [ReferenceFinder documentation](../../ReferenceFinder/README.md) for more details.**

