# GMLTool

Trimming Large CityGML files and exporting to Wavefront OBJ files

## Feature

- Load CityGML files
- Extract City Object within a specific positional range.
- Output the final CityGML
- Export CityGML to Wavefront OBJ files
- Large file supported. Streaming process in order to overcome the memory bottleneck.
- Multi-thread supporting

## Usage

```
> GMLTool -h
Description:
  GML tool

Usage:
  GMLTool <input> [options]

Arguments:
  <input>  Input GML file

Options:
  --max-obj <max-obj>              Maximum number of City Objects to extract (-1 = unlimited) [default: -1]
  --num-obj-total <num-obj-total>  Number of City Objects in the GML input file (-1 = unknown, no progress will be
                                   shown) [default: -1]
  --range                          Extract City Objects from a specific positional range [default: False]
  --x-min <x-min>                  Range: X Min [default: 0]
  --x-max <x-max>                  Range: X Max [default: 0]
  --y-min <y-min>                  Range: Y Min [default: 0]
  --y-max <y-max>                  Range: y Max [default: 0]
  --out-gml <out-gml>              Output GML file []
  --out-obj <out-obj>              Output OBJ file []
  --thread <thread>                Number of threads for processing [default: 32767]
  --version                        Show version information
  -?, -h, --help                   Show help and usage information

```