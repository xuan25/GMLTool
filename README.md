# GMLTool

Probing, Plotting and Trimming Large CityGML files. Exporting City Objects to Wavefront OBJ files.

<img src="img/example.png" width="400"/>

## Feature

- Load and Probing CityGML files
- Extract City Object within a specific region.
- Plotting 2D image of the CityGML
- Output the trimmed final CityGML
- Export CityGML to Wavefront OBJ files with optional mesh merging.
- Large file supported. Streaming process in order to overcome the memory bottleneck.
- Multi-thread supporting

## Usage

### General

```
Description:
  GML tool

Usage:
  GMLTool <input> [command] [options]

Arguments:
  <input>  Input GML file

Options:
  --version       Show version information
  -?, -h, --help  Show help and usage information


Commands:
  --probe                                                           Probe the metadata of the GML file, no file output
  --plot <width> <height> <x-min> <x-max> <y-min> <y-max> <output>  Plot a 2D image of the city
  --export                                                          Export from CityGML
```

### Probe

```
Description:
  Probe the metadata of the GML file, no output

Usage:
  GMLTool <input> --probe [options]

Arguments:
  <input>  Input GML file

Options:
  --threads <threads>  Maximum number of threads for processing [default: 2]
  -?, -h, --help       Show help and usage information
```

#### Example Result

```
Number of City Objects: 1083437
Number of vertices: 74767248
Number of faces: 12965608
Range of X: [278344.415096403 - 325334.16689638]
Range of Y: [36879.3363186475 - 83109.6990185874]
Range of Z: [-11.8920701041398 - 547.759187198374]
```

### Plot

```
Description:
  Plot a 2D image of the city

Usage:
  GMLTool <input> --plot <width> <height> [<x-min> [<x-max> [<y-min> [<y-max> <output>]]]] [options]

Arguments:
  <input>   Input GML file
  <width>   Width of the plot in pixel
  <height>  Height of the plot in pixel
  <x-min>   Region: X Min
  <x-max>   Region: X Max
  <y-min>   Region: Y Min
  <y-max>   Region: Y Max
  <output>  Output plotting image file

Options:
  --max-obj <max-obj>              Maximum number of City Objects to extract (-1 = unlimited) [default: -1]
  --num-obj-total <num-obj-total>  Number of City Objects in the GML input file (-1 = unknown, no progress will be
                                   shown) [default: -1]
  --threads <threads>              Maximum number of threads for processing [default: 2]
  -?, -h, --help                   Show help and usage information
```

### Export

```
Description:
  Export from CityGML

Usage:
  GMLTool <input> --export [command] [options]

Arguments:
  <input>  Input GML file

Options:
  --out-gml <out-gml>              Output GML file []
  --merge-mesh                     Merge City Objects to a single mesh in the OBJ file [default: False]
  --out-obj <out-obj>              Output OBJ file []
  --max-obj <max-obj>              Maximum number of City Objects to extract (-1 = unlimited) [default: -1]
  --num-obj-total <num-obj-total>  Number of City Objects in the GML input file (-1 = unknown, no progress will be
                                   shown) [default: -1]
  --threads <threads>              Maximum number of threads for processing [default: 2]
  -?, -h, --help                   Show help and usage information


Commands:
  --region <x-min> <x-max> <y-min> <y-max>  Extract City Objects from a sub-region
```

## Run Examples

1. Download and place CityGML data into `Data/`. See [Data/README.md](Data/README.md) for more detail. 
2. Review launch settings and command-line parameters in `GMLTool/GMLTool/Properties/launchSettings.json`.
3. Open the solution with Visual Studio, then run the launch settings start with `Example`.
4. Check output files in `Data/`.

| <img src="img/nyc_buildings.png" width="400"/> | <img src="img/nyc_roads.png" width="400"/> |
|---|---|
| Plotting of Buildings | Plotting of Roads |

| <img src="img/example.png" width="400"/> | <img src="img/example2.png" width="400"/> |
|---|---|
| Exported OBJ Rendering 1 | Exported OBJ Rendering 2 |
