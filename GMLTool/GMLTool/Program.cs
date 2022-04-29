/*
 * MIT License
 * 
 * Copyright (c) 2022 Xuan25
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
*/

using System.CommandLine;
using System.Drawing;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.XPath;

namespace GMLTool
{
    class Program
    {
        static int Main(params string[] args)
        {
            using (StreamReader licenseReader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("GMLTool.LICENSE")))
            {
                Console.WriteLine("========================");
                Console.WriteLine(licenseReader.ReadToEnd());
                Console.WriteLine("========================");
                Console.WriteLine();
            }

            Argument inputArgument = new Argument<FileInfo>(
                    "input",
                    "Input GML file").ExistingOnly();

            Option maxObjOption = new Option<int>(
                    "--max-obj",
                    getDefaultValue: () => -1,
                    description: "Maximum number of City Objects to extract (-1 = unlimited)");
            Option numObjTotalOption = new Option<int>(
                    "--num-obj-total",
                    getDefaultValue: () => -1,
                    description: "Number of City Objects in the GML input file (-1 = unknown, no progress will be shown)");

            Option rangeOption = new Option<bool>(
                    "--range",
                    getDefaultValue: () => false,
                    "Extract City Objects from a specific positional range");

            Argument xMinArgument = new Argument<double>(
                                "x-min",
                                description: "Range: X Min");
            Argument xMaxArgument = new Argument<double>(
                    "x-max",
                    description: "Range: X Max");
            Argument yMinArgument = new Argument<double>(
                    "y-min",
                    description: "Range: Y Min");
            Argument yMaxArgument = new Argument<double>(
                    "y-max",
                    description: "Range: y Max");

            Option outputGMLOption = new Option<FileInfo?>(
                    "--out-gml",
                    getDefaultValue: () => null,
                    "Output GML file");

            Option mergeMeshOption = new Option<bool>(
                    "--merge-mesh",
                    getDefaultValue: () => false,
                    "Merge City Objects to a single mesh in the OBJ file");
            Option outputOBJOption = new Option<FileInfo?>(
                    "--out-obj",
                    getDefaultValue: () => null,
                    "Output OBJ file");

            Argument plotWidthArgument = new Argument<int>(
                    "width",
                    description: "Width of the plot in pixel");
            Argument plotHeightArgument = new Argument<int>(
                    "height",
                    description: "Height of the plot in pixel");

            Argument outputPlotArgument = new Argument<FileInfo?>(
                    "output",
                    "Output plotting image file");
            Option threadsOption = new Option<int>(
                    "--threads",
                    getDefaultValue: () =>
                    {
                        ThreadPool.GetMaxThreads(out int workerThreads, out int copletionPortThreads);
                        return workerThreads;
                    },
                    "Maximum number of threads for processing");

            Command probeCommand = new Command("--probe", "Probe the metadata of the GML file, no output")
            {
                threadsOption,
            };

            Command plotCommand = new Command("--plot", "Plot a 2D image of the city")
            {
                
                plotWidthArgument,
                plotHeightArgument,
                xMinArgument,
                xMaxArgument,
                yMinArgument,
                yMaxArgument,
                outputPlotArgument,
                maxObjOption,
                numObjTotalOption,
                threadsOption,
            };

            Command exportRangeCommand = new Command("--range", "Extract City Objects from a specific positional range")
            {
                xMinArgument,
                xMaxArgument,
                yMinArgument,
                yMaxArgument,
            };

            Command exportCommand = new Command("--export", "Export from CityGML")
            {
                outputGMLOption,
                mergeMeshOption,
                outputOBJOption,
                maxObjOption,
                numObjTotalOption,
                threadsOption,
                exportRangeCommand,
            };

            RootCommand rootCommand = new RootCommand
            {
                inputArgument,
                probeCommand,
                plotCommand,
                exportCommand,
            };
            rootCommand.Description = "GML tool";

            rootCommand.SetHandler(() =>
            {
                Console.WriteLine("Please select an action (probe, plot, export)");
            });

            probeCommand.SetHandler((FileInfo input, int threads) =>
            {
                Probe(input, threads);
            }, inputArgument, threadsOption);

            plotCommand.SetHandler((FileInfo input, int width, int height, double xMin, double xMax, double yMin, double yMax, FileInfo outputPlot, int maxObj, int numObjTotal, int threads) =>
            {
                Plot(input, width, height, xMin, xMax, yMin, yMax, outputPlot, numObjTotal, threads);
            }, inputArgument, plotWidthArgument, plotHeightArgument, xMinArgument, xMaxArgument, yMinArgument, yMaxArgument, outputPlotArgument, maxObjOption, numObjTotalOption, threadsOption);

            exportCommand.SetHandler((FileInfo input, FileInfo? outputGML, bool mergeMesh, FileInfo? outputOBJ, int maxObj, int numObjTotal, int threads) =>
            {
                Export(input, outputGML, mergeMesh, outputOBJ, maxObj, numObjTotal, threads, false, double.NaN, double.NaN, double.NaN, double.NaN);
            }, inputArgument, outputGMLOption, mergeMeshOption, outputOBJOption, maxObjOption, numObjTotalOption, threadsOption);

            exportRangeCommand.SetHandler((FileInfo input, FileInfo? outputGML, bool mergeMesh, FileInfo? outputOBJ, int maxObj, int numObjTotal, int threads, double xMin, double xMax, double yMin, double yMax) =>
            {
                Export(input, outputGML, mergeMesh, outputOBJ, maxObj, numObjTotal, threads, true, xMin, xMax, yMin, yMax);
            }, inputArgument, outputGMLOption, mergeMeshOption, outputOBJOption, maxObjOption, numObjTotalOption, threadsOption, xMinArgument, xMaxArgument, yMinArgument, yMaxArgument);

            return rootCommand.Invoke(args);
        }

        static void Probe(FileInfo input, int threads)
        {
            ProgressBar progressBar = new ProgressBar();

            ThreadPool.SetMaxThreads(threads, 2);

            XmlReader gmlReader = XmlReader.Create(input.FullName, new XmlReaderSettings() { DtdProcessing = DtdProcessing.Parse });

            int numObjRead = 0;

            int vertexIdx = 1;
            int faceIdx = 1;

            double lx = double.MaxValue, ly = double.MaxValue, lz = double.MaxValue, ux = double.MinValue, uy = double.MinValue, uz = double.MinValue;

            while (gmlReader.Read())
            {
                if (gmlReader.NodeType == XmlNodeType.Element && gmlReader.LocalName == "CityModel" && gmlReader.NamespaceURI == "http://www.opengis.net/citygml/2.0")
                {
                    XmlReader cityModelReader = XmlReader.Create(gmlReader, null);
                    cityModelReader.Read();

                    int numPendingWork = 0;
                    while (cityModelReader.Read() &&
                        !(cityModelReader.NodeType == XmlNodeType.EndElement && cityModelReader.LocalName == "CityModel" && cityModelReader.NamespaceURI == "http://www.opengis.net/citygml/2.0"))
                    {
                        if (cityModelReader.NodeType == XmlNodeType.Element && cityModelReader.LocalName == "cityObjectMember" && cityModelReader.NamespaceURI == "http://www.opengis.net/citygml/2.0")
                        {
                            // extract object content

                            string memberStr = cityModelReader.ReadOuterXml();
                            Interlocked.Increment(ref numPendingWork);

                            // wait for other threads
                            while (ThreadPool.PendingWorkItemCount > threads * 2)
                            {
                                Thread.Sleep(10);
                            }

                            ThreadPool.QueueUserWorkItem((obj) =>
                            {
                                XmlReader memberReader = XmlReader.Create(new StringReader(memberStr), null);
                                XPathDocument memberDocument = new XPathDocument(memberReader);
                                XPathNavigator memberNavigator = memberDocument.CreateNavigator();

                                // range validation
                                bool hasBoundary = true;

                                // find existing boundary

                                if (memberNavigator.MoveToChild("cityObjectMember", "http://www.opengis.net/citygml/2.0") &&
                                    memberNavigator.MoveToFirstChild() &&   // Building etc.
                                    memberNavigator.MoveToChild("boundedBy", "http://www.opengis.net/gml") &&
                                    memberNavigator.MoveToChild("Envelope", "http://www.opengis.net/gml") &&
                                    memberNavigator.MoveToChild("lowerCorner", "http://www.opengis.net/gml"))
                                {
                                    string val = memberNavigator.Value;
                                    string[] vals = val.Split(' ');
                                    lx = Math.Min(lx, double.Parse(vals[0]));
                                    ly = Math.Min(ly, double.Parse(vals[1]));
                                    lz = Math.Min(lz, double.Parse(vals[2]));
                                }
                                else
                                {
                                    hasBoundary = false;
                                }
                                memberNavigator.MoveToRoot();

                                if (memberNavigator.MoveToChild("cityObjectMember", "http://www.opengis.net/citygml/2.0") &&
                                    memberNavigator.MoveToFirstChild() &&   // Building, Road etc.
                                    memberNavigator.MoveToChild("boundedBy", "http://www.opengis.net/gml") &&
                                    memberNavigator.MoveToChild("Envelope", "http://www.opengis.net/gml") &&
                                    memberNavigator.MoveToChild("upperCorner", "http://www.opengis.net/gml"))
                                {
                                    string val = memberNavigator.Value;
                                    string[] vals = val.Split(' ');
                                    ux = Math.Max(ux, double.Parse(vals[0]));
                                    uy = Math.Max(uy, double.Parse(vals[1]));
                                    uz = Math.Max(uz, double.Parse(vals[2]));
                                }
                                else
                                {
                                    hasBoundary = false;
                                }
                                memberNavigator.MoveToRoot();

                                // fallback: boundary detection

                                if (!hasBoundary)
                                {
                                    XmlReader memberObjBuffer = XmlReader.Create(new StringReader(memberStr), null);
                                    while (memberObjBuffer.Read())
                                    {
                                        if (memberObjBuffer.NodeType == XmlNodeType.Element && memberObjBuffer.LocalName == "posList" && memberObjBuffer.NamespaceURI == "http://www.opengis.net/gml")
                                        {
                                            string val = memberObjBuffer.ReadElementContentAsString();
                                            string[] vals = val.Split(' ');

                                            // vertex
                                            for (int i = 0; i < vals.Length; i += 3)
                                            {
                                                double x = double.Parse(vals[i]);
                                                double y = double.Parse(vals[i + 1]);
                                                double z = double.Parse(vals[i + 2]);

                                                lx = Math.Min(lx, x);
                                                ly = Math.Min(ly, y);
                                                lz = Math.Min(lz, z);

                                                ux = Math.Max(ux, x);
                                                uy = Math.Max(uy, y);
                                                uz = Math.Max(uz, z);
                                            }

                                            hasBoundary = true;
                                        }
                                    }
                                }

                                if (hasBoundary)
                                {
                                    // probe object
                                    XmlReader memberObjBuffer = XmlReader.Create(new StringReader(memberStr), null);
                                    while (memberObjBuffer.Read())
                                    {
                                        if (memberObjBuffer.NodeType == XmlNodeType.Element && memberObjBuffer.LocalName == "posList" && memberObjBuffer.NamespaceURI == "http://www.opengis.net/gml")
                                        {
                                            string val = memberObjBuffer.ReadElementContentAsString();
                                            string[] vals = val.Split(' ');

                                            //int vertexIdxStart = vertexIdx;

                                            // vertex
                                            Interlocked.Add(ref vertexIdx, vals.Length / 3);

                                            // face
                                            Interlocked.Increment(ref vertexIdx);
                                        }
                                    }
                                }

                                Interlocked.Increment(ref numObjRead);

                                // progress display

                                progressBar.Template($"[{numObjRead}] Probing... {{Icon}}");

                                Interlocked.Decrement(ref numPendingWork);
                            });
                        }
                    }

                    while (numPendingWork != 0)
                    {
                        Thread.Sleep(100);
                    }
                    
                }
            }

            progressBar.Dispose();

            Console.WriteLine($"Finished");
            Console.WriteLine();
            Console.WriteLine($"Number of City Objects: {numObjRead}");
            Console.WriteLine($"Number of vertices: {vertexIdx - 1}");
            Console.WriteLine($"Number of faces: {faceIdx - 1}");
            Console.WriteLine($"Range of X: [{lx} - {ux}]");
            Console.WriteLine($"Range of Y: [{ly} - {uy}]");
            Console.WriteLine($"Range of Z: [{lz} - {uz}]");
        }

        static void Plot(FileInfo input, int width, int height, double xMin, double xMax, double yMin, double yMax, FileInfo output, int numObjTotal, int threads)
        {
            ProgressBar progressBar = new ProgressBar();

            ThreadPool.SetMaxThreads(threads, 2);

            XmlReader gmlReader = XmlReader.Create(input.FullName, new XmlReaderSettings() { DtdProcessing = DtdProcessing.Parse });

            int numObjRead = 0;
            int numObjPloted = 0;

            int vertexIdx = 1;
            int faceIdx = 1;

            Bitmap bitmap = new Bitmap(width, height);
            Graphics graphics = Graphics.FromImage(bitmap);
            Brush brush = new SolidBrush(Color.FromArgb(255, 255, 255, 255));

            while (gmlReader.Read())
            {
                if (gmlReader.NodeType == XmlNodeType.Element && gmlReader.LocalName == "CityModel" && gmlReader.NamespaceURI == "http://www.opengis.net/citygml/2.0")
                {
                    XmlReader cityModelReader = XmlReader.Create(gmlReader, null);
                    cityModelReader.Read();

                    int numPendingWork = 0;
                    while (cityModelReader.Read() &&
                        !(cityModelReader.NodeType == XmlNodeType.EndElement && cityModelReader.LocalName == "CityModel" && cityModelReader.NamespaceURI == "http://www.opengis.net/citygml/2.0"))
                    {
                        if (cityModelReader.NodeType == XmlNodeType.Element && cityModelReader.LocalName == "cityObjectMember" && cityModelReader.NamespaceURI == "http://www.opengis.net/citygml/2.0")
                        {
                            // extract object content

                            string memberStr = cityModelReader.ReadOuterXml();
                            Interlocked.Increment(ref numPendingWork);

                            // wait for other threads
                            while (ThreadPool.PendingWorkItemCount > threads * 2)
                            {
                                Thread.Sleep(10);
                            }

                            ThreadPool.QueueUserWorkItem((obj) =>
                            {
                                XmlReader memberReader = XmlReader.Create(new StringReader(memberStr), null);
                                XPathDocument memberDocument = new XPathDocument(memberReader);
                                XPathNavigator memberNavigator = memberDocument.CreateNavigator();

                                // range validation
                                bool hasBoundary = true;
                                double lx = double.MaxValue, ly = double.MaxValue, lz = double.MaxValue, ux = double.MinValue, uy = double.MinValue, uz = double.MinValue;

                                // find existing boundary

                                if (memberNavigator.MoveToChild("cityObjectMember", "http://www.opengis.net/citygml/2.0") &&
                                    memberNavigator.MoveToFirstChild() &&   // Building etc.
                                    memberNavigator.MoveToChild("boundedBy", "http://www.opengis.net/gml") &&
                                    memberNavigator.MoveToChild("Envelope", "http://www.opengis.net/gml") &&
                                    memberNavigator.MoveToChild("lowerCorner", "http://www.opengis.net/gml"))
                                {
                                    string val = memberNavigator.Value;
                                    string[] vals = val.Split(' ');
                                    lx = double.Parse(vals[0]);
                                    ly = double.Parse(vals[1]);
                                    lz = double.Parse(vals[2]);
                                }
                                else
                                {
                                    hasBoundary = false;
                                }
                                memberNavigator.MoveToRoot();

                                if (memberNavigator.MoveToChild("cityObjectMember", "http://www.opengis.net/citygml/2.0") &&
                                    memberNavigator.MoveToFirstChild() &&   // Building, Road etc.
                                    memberNavigator.MoveToChild("boundedBy", "http://www.opengis.net/gml") &&
                                    memberNavigator.MoveToChild("Envelope", "http://www.opengis.net/gml") &&
                                    memberNavigator.MoveToChild("upperCorner", "http://www.opengis.net/gml"))
                                {
                                    string val = memberNavigator.Value;
                                    string[] vals = val.Split(' ');
                                    ux = double.Parse(vals[0]);
                                    uy = double.Parse(vals[1]);
                                    uz = double.Parse(vals[2]);
                                }
                                else
                                {
                                    hasBoundary = false;
                                }
                                memberNavigator.MoveToRoot();

                                // fallback: boundary detection

                                if (!hasBoundary)
                                {
                                    XmlReader memberObjBuffer = XmlReader.Create(new StringReader(memberStr), null);
                                    while (memberObjBuffer.Read())
                                    {
                                        if (memberObjBuffer.NodeType == XmlNodeType.Element && memberObjBuffer.LocalName == "posList" && memberObjBuffer.NamespaceURI == "http://www.opengis.net/gml")
                                        {
                                            string val = memberObjBuffer.ReadElementContentAsString();
                                            string[] vals = val.Split(' ');

                                            // vertex
                                            for (int i = 0; i < vals.Length; i += 3)
                                            {
                                                double x = double.Parse(vals[i]);
                                                double y = double.Parse(vals[i + 1]);
                                                double z = double.Parse(vals[i + 2]);

                                                lx = Math.Min(lx, x);
                                                ly = Math.Min(ly, y);
                                                lz = Math.Min(lz, z);

                                                ux = Math.Max(ux, x);
                                                uy = Math.Max(uy, y);
                                                uz = Math.Max(uz, z);
                                            }

                                            hasBoundary = true;
                                        }
                                    }
                                }

                                bool isInvalidRange = ((lx < xMin && ux < xMin) || (lx > xMax && ux > xMax) || (ly < yMin && uy < yMin) || (ly > yMax && uy > yMax));
                                if (!hasBoundary || !isInvalidRange)
                                {
                                    // plot object
                                    XmlReader memberObjBuffer = XmlReader.Create(new StringReader(memberStr), null);
                                    while (memberObjBuffer.Read())
                                    {
                                        if (memberObjBuffer.NodeType == XmlNodeType.Element && memberObjBuffer.LocalName == "posList" && memberObjBuffer.NamespaceURI == "http://www.opengis.net/gml")
                                        {
                                            string val = memberObjBuffer.ReadElementContentAsString();
                                            string[] vals = val.Split(' ');

                                            PointF[] points = new PointF[vals.Length / 3];
                                            
                                            // vertex
                                            for (int i = 0; i < vals.Length; i += 3)
                                            {
                                                double x = double.Parse(vals[i]);
                                                double y = double.Parse(vals[i+1]);
                                                double z = double.Parse(vals[i+2]);

                                                double xP = (x - xMin) / (xMax - xMin) * width;
                                                double yP = (y - yMin) / (yMax - yMin) * height;

                                                points[i / 3] = new PointF((float)xP, (float)yP);
                                            }
                                            Interlocked.Add(ref vertexIdx, vals.Length / 3);

                                            // face
                                            lock(graphics)
                                            {
                                                graphics.FillPolygon(brush, points);
                                            }
                                            Interlocked.Increment(ref faceIdx);
                                        }
                                    }
                                    Interlocked.Increment(ref numObjPloted);
                                }
                                Interlocked.Increment(ref numObjRead);

                                // progress display

                                if (numObjTotal > 0)
                                {
                                    double ratio = (numObjRead / (double)numObjTotal);
                                    progressBar.Report(ratio);
                                    progressBar.Template($"[{{Progress}}] {{Bar}} {{Icon}} {numObjPloted} City Object ploted");
                                }
                                else
                                {
                                    progressBar.Template($"[{numObjRead}] {{Icon}} {numObjPloted} City Object ploted");
                                }

                                Interlocked.Decrement(ref numPendingWork);
                            });
                        }
                    }

                    while (numPendingWork != 0)
                    {
                        Thread.Sleep(100);
                    }
                }
            }

            progressBar.Dispose();

            bitmap.Save(output.FullName);

            Console.WriteLine($"Finished");
            Console.WriteLine();
            Console.WriteLine($"Number of City Objects: {numObjRead}");
            Console.WriteLine($"Number of vertics: {vertexIdx - 1}");
            Console.WriteLine($"Number of faces: {faceIdx - 1}");
        }

        static void Export(FileInfo input, FileInfo? outputGML, bool mergeMesh, FileInfo? outputOBJ, int maxObj, int numObjTotal, int threads, bool subRange, double xMin, double xMax, double yMin, double yMax)
        {
            ProgressBar progressBar = new ProgressBar();

            bool isOutputGML = outputGML != null;
            bool isOutputOBJ = outputOBJ != null;

            ThreadPool.SetMaxThreads(threads, 2);

            XmlReader gmlReader = XmlReader.Create(input.FullName, new XmlReaderSettings() { DtdProcessing = DtdProcessing.Parse });

            int numObjRead = 0;
            int numObjExported = 0;

            int vertexIdx = 1;
            int faceIdx = 1;

            XmlWriter? gmlWriter = null;
            if (isOutputGML)
            {
                gmlWriter = XmlWriter.Create(outputGML.FullName, new XmlWriterSettings() { Indent = true });
            }

            FileStream? objVertexFileStream = null;
            FileStream? objFaceFileStream = null;
            StreamWriter? objVertexWriter = null;
            StreamWriter? objFaceWriter = null;

            if (isOutputOBJ)
            {
                objVertexFileStream = File.Create($"{outputOBJ.FullName}.vert", 4096, FileOptions.DeleteOnClose | FileOptions.SequentialScan);
                objFaceFileStream = File.Create($"{outputOBJ.FullName}.face", 4096, FileOptions.DeleteOnClose | FileOptions.SequentialScan);

                objVertexWriter = new StreamWriter(objVertexFileStream, Encoding.ASCII, -1, true);
                objFaceWriter = new StreamWriter(objFaceFileStream, Encoding.ASCII, -1, true);
            }

            while (gmlReader.Read() && (numObjExported < maxObj || maxObj < 0))
            {
                if (gmlReader.NodeType == XmlNodeType.Element && gmlReader.LocalName == "CityModel" && gmlReader.NamespaceURI == "http://www.opengis.net/citygml/2.0")
                {
                    XmlReader cityModelReader = XmlReader.Create(gmlReader, null);
                    cityModelReader.Read();

                    if (isOutputGML)
                    {
                        gmlWriter.WriteStartElement(cityModelReader.Prefix, cityModelReader.LocalName, cityModelReader.NamespaceURI);
                        gmlWriter.WriteAttributes(cityModelReader, true);
                    }


                    int numPendingWork = 0;
                    while (cityModelReader.Read() &&
                        !(cityModelReader.NodeType == XmlNodeType.EndElement && cityModelReader.LocalName == "CityModel" && cityModelReader.NamespaceURI == "http://www.opengis.net/citygml/2.0") &&
                        (numObjExported < maxObj || maxObj < 0))
                    {
                        if (cityModelReader.NodeType == XmlNodeType.Element && cityModelReader.LocalName == "cityObjectMember" && cityModelReader.NamespaceURI == "http://www.opengis.net/citygml/2.0")
                        {
                            // extract object content

                            string memberStr = cityModelReader.ReadOuterXml();

                            Interlocked.Increment(ref numPendingWork);

                            // wait for other threads
                            while(ThreadPool.PendingWorkItemCount > threads * 2)
                            {
                                Thread.Sleep(10);
                            }

                            ThreadPool.QueueUserWorkItem((obj) =>
                            {
                                XmlReader memberReader = XmlReader.Create(new StringReader(memberStr), null);
                                XPathDocument memberDocument = new XPathDocument(memberReader);
                                XPathNavigator memberNavigator = memberDocument.CreateNavigator();

                                // range validation
                                bool hasBoundary = true;
                                double lx = double.MaxValue, ly = double.MaxValue, lz = double.MaxValue, ux = double.MinValue, uy = double.MinValue, uz = double.MinValue;

                                // find existing boundary

                                if (memberNavigator.MoveToChild("cityObjectMember", "http://www.opengis.net/citygml/2.0") &&
                                    memberNavigator.MoveToFirstChild() &&   // Building etc.
                                    memberNavigator.MoveToChild("boundedBy", "http://www.opengis.net/gml") &&
                                    memberNavigator.MoveToChild("Envelope", "http://www.opengis.net/gml") &&
                                    memberNavigator.MoveToChild("lowerCorner", "http://www.opengis.net/gml"))
                                {
                                    string val = memberNavigator.Value;
                                    string[] vals = val.Split(' ');
                                    lx = double.Parse(vals[0]);
                                    ly = double.Parse(vals[1]);
                                    lz = double.Parse(vals[2]);
                                }
                                else
                                {
                                    hasBoundary = false;
                                }
                                memberNavigator.MoveToRoot();

                                if (memberNavigator.MoveToChild("cityObjectMember", "http://www.opengis.net/citygml/2.0") &&
                                    memberNavigator.MoveToFirstChild() &&   // Building, Road etc.
                                    memberNavigator.MoveToChild("boundedBy", "http://www.opengis.net/gml") &&
                                    memberNavigator.MoveToChild("Envelope", "http://www.opengis.net/gml") &&
                                    memberNavigator.MoveToChild("upperCorner", "http://www.opengis.net/gml"))
                                {
                                    string val = memberNavigator.Value;
                                    string[] vals = val.Split(' ');
                                    ux = double.Parse(vals[0]);
                                    uy = double.Parse(vals[1]);
                                    uz = double.Parse(vals[2]);
                                }
                                else
                                {
                                    hasBoundary = false;
                                }
                                memberNavigator.MoveToRoot();

                                // fallback: boundary detection

                                if (!hasBoundary)
                                {
                                    XmlReader memberObjBuffer = XmlReader.Create(new StringReader(memberStr), null);
                                    while (memberObjBuffer.Read())
                                    {
                                        if (memberObjBuffer.NodeType == XmlNodeType.Element && memberObjBuffer.LocalName == "posList" && memberObjBuffer.NamespaceURI == "http://www.opengis.net/gml")
                                        {
                                            string val = memberObjBuffer.ReadElementContentAsString();
                                            string[] vals = val.Split(' ');

                                            // vertex
                                            for (int i = 0; i < vals.Length; i += 3)
                                            {
                                                double x = double.Parse(vals[i]);
                                                double y = double.Parse(vals[i + 1]);
                                                double z = double.Parse(vals[i + 2]);

                                                lx = Math.Min(lx, x);
                                                ly = Math.Min(ly, y);
                                                lz = Math.Min(lz, z);

                                                ux = Math.Max(ux, x);
                                                uy = Math.Max(uy, y);
                                                uz = Math.Max(uz, z);
                                            }

                                            hasBoundary = true;
                                        }
                                    }
                                }

                                // obj ID

                                string objID = null;
                                if (memberNavigator.MoveToChild("cityObjectMember", "http://www.opengis.net/citygml/2.0") &&
                                    memberNavigator.MoveToFirstChild())   // Building, Road etc.
                                {
                                    objID = memberNavigator.GetAttribute("id", "http://www.opengis.net/gml");
                                }
                                memberNavigator.MoveToRoot();

                                if (objID == null)
                                {
                                    objID = $"Generated_{Guid.NewGuid()}";
                                }

                                // obj name

                                string objName = string.Empty;
                                if (memberNavigator.MoveToChild("cityObjectMember", "http://www.opengis.net/citygml/2.0") &&
                                    memberNavigator.MoveToFirstChild() &&
                                    memberNavigator.MoveToChild("name", "http://www.opengis.net/gml"))   // Building, Road etc.
                                {
                                    objName = memberNavigator.InnerXml;
                                }
                                memberNavigator.MoveToRoot();

                                bool isInvalidRange = subRange && ((lx < xMin && ux < xMin) || (lx > xMax && ux > xMax) || (ly < yMin && uy < yMin) || (ly > yMax && uy > yMax));
                                if (!hasBoundary || !isInvalidRange)
                                {
                                    // validate max number of objects
                                    if (!(numObjExported < maxObj || maxObj < 0))
                                    {
                                        return;
                                    }
                                    Interlocked.Increment(ref numObjExported);

                                    // export OBJ
                                    if (isOutputOBJ)
                                    {
                                        XmlReader memberObjBuffer = XmlReader.Create(new StringReader(memberStr), null);
                                        lock (objFaceWriter)
                                        {
                                            if (!mergeMesh)
                                            {
                                                objFaceWriter.WriteLine($"o {objID}/{objName}");
                                            }
                                            while (memberObjBuffer.Read())
                                            {
                                                if (memberObjBuffer.NodeType == XmlNodeType.Element && memberObjBuffer.LocalName == "posList" && memberObjBuffer.NamespaceURI == "http://www.opengis.net/gml")
                                                {
                                                    string val = memberObjBuffer.ReadElementContentAsString();
                                                    string[] vals = val.Split(' ');

                                                    int vertexIdxStart = vertexIdx;

                                                    // vertex
                                                    for (int i = 0; i < vals.Length; i += 3)
                                                    {
                                                        objVertexWriter.WriteLine($"v {double.Parse(vals[i])} {double.Parse(vals[i + 2])} {-double.Parse(vals[i + 1])}");
                                                        vertexIdx++;
                                                    }

                                                    // face
                                                    objFaceWriter.Write("f");
                                                    for (int i = vertexIdxStart; i < vertexIdx; i++)
                                                    {
                                                        objFaceWriter.Write($" {i}");
                                                    }
                                                    objFaceWriter.WriteLine();
                                                }
                                            }
                                        }
                                    }

                                    // export CityGML
                                    if (isOutputGML)
                                    {
                                        lock (gmlWriter)
                                        {
                                            XmlReader memberBuffer = XmlReader.Create(new StringReader(memberStr), null);
                                            gmlWriter.WriteNode(memberBuffer, false);
                                        }
                                    }

                                }

                                Interlocked.Increment(ref numObjRead);

                                // progress display

                                if (numObjTotal > 0)
                                {
                                    double ratio = (numObjRead / (double)numObjTotal);
                                    progressBar.Template($"[{{Progress}}] {{Bar}} {{Icon}} {numObjExported} City Object ploted");
                                }
                                else
                                {
                                    progressBar.Template($"[{numObjRead}] {{Icon}} {numObjExported} City Object ploted");
                                }

                                Interlocked.Decrement(ref numPendingWork);
                            });
                        }
                        else if (cityModelReader.NodeType != XmlNodeType.Whitespace)
                        {
                            if (isOutputGML)
                            {
                                lock (gmlWriter)
                                {
                                    gmlWriter.WriteNode(gmlReader, false);
                                }
                            }

                        }
                    }

                    while (numPendingWork != 0)
                    {
                        Thread.Sleep(100);
                    }

                    if (isOutputGML)
                    {
                        gmlWriter.WriteEndElement();
                    }

                }
                else if (gmlReader.NodeType == XmlNodeType.XmlDeclaration)
                {
                    if (isOutputGML)
                    {
                        gmlWriter.WriteNode(gmlReader, false);
                        gmlWriter.WriteComment(" Modified by GML toolkit. Author: Xuan25 (Github). ");
                    }

                }
                else
                {
                    if (isOutputGML)
                    {
                        gmlWriter.WriteNode(gmlReader, false);
                    }
                }
            }

            if (isOutputGML)
            {
                gmlWriter?.WriteEndDocument();
                gmlWriter?.Close();
            }

            if (isOutputOBJ)
            {
                objVertexWriter.Close();
                objFaceWriter.Close();

                objVertexFileStream.Position = 0;
                objFaceFileStream.Position = 0;

                FileStream objFileStream = new FileStream(outputOBJ.FullName, FileMode.Create);
                StreamWriter objWriter = new StreamWriter(objFileStream, Encoding.ASCII, -1, true);

                objWriter.WriteLine($"# Write by GML toolkit. Author: Xuan25 (Github).");
                objWriter.WriteLine($"# Geometric vertices.");
                objWriter.WriteLine();
                objWriter.Flush();
                objVertexFileStream.CopyTo(objFileStream);
                objWriter.WriteLine();
                objWriter.WriteLine($"# Polygonal face element.");
                objWriter.WriteLine();
                objWriter.Flush();
                objFaceFileStream.CopyTo(objFileStream);

                objWriter.Close();
                objFileStream.Close();

                objVertexFileStream.Close();
                objFaceFileStream.Close();
            }

            progressBar.Dispose();

            Console.WriteLine($"Finished: {numObjRead} Objects Read; {numObjExported} Objects Exported");
        }
    }
}