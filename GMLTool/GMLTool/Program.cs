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

            var inputArgument = new Argument<FileInfo>(
                    "input",
                    "Input GML file").ExistingOnly();
            var maxObjOption = new Option<int>(
                    "--max-obj",
                    getDefaultValue: () => -1,
                    description: "Maximum number of City Objects to extract (-1 = unlimited)");
            var numObjTotalOption = new Option<int>(
                    "--num-obj-total",
                    getDefaultValue: () => -1,
                    description: "Number of City Objects in the GML input file (-1 = unknown, no progress will be shown)");
            var rangeOption = new Option<bool>(
                    "--range",
                    getDefaultValue: () => false,
                    "Extract City Objects from a specific positional range");
            var xMinOption = new Option<double>(
                    "--x-min",
                    getDefaultValue: () => 0,
                    description: "Range: X Min");
            var xMaxOption = new Option<double>(
                    "--x-max",
                    getDefaultValue: () => 0,
                    description: "Range: X Max");
            var yMinOption = new Option<double>(
                    "--y-min",
                    getDefaultValue: () => 0,
                    description: "Range: Y Min");
            var yMaxOption = new Option<double>(
                    "--y-max",
                    getDefaultValue: () => 0,
                    description: "Range: y Max");
            var outputGMLOption = new Option<FileInfo?>(
                    "--out-gml",
                    getDefaultValue: () => null,
                    "Output GML file");
            var outputOBJOption = new Option<FileInfo?>(
                    "--out-obj",
                    getDefaultValue: () => null,
                    "Output OBJ file");
            var threadOption = new Option<int>(
                    "--thread",
                    getDefaultValue: () =>
                    {
                        ThreadPool.GetMaxThreads(out int workerThreads, out int copletionPortThreads);
                        return workerThreads;
                    },
                    "Number of threads for processing");

            var rootCommand = new RootCommand
            {
                inputArgument,
                maxObjOption,
                numObjTotalOption,
                rangeOption,
                xMinOption,
                xMaxOption,
                yMinOption,
                yMaxOption,
                outputGMLOption,
                outputOBJOption,
                threadOption,
            };

            rootCommand.Description = "GML tool";

            rootCommand.SetHandler((FileInfo input, int maxObj, int numObjTotal, bool subRange, double xMin, double xMax, double yMin, double yMax, FileInfo? outputGML, FileInfo? outputOBJ, int thread) =>
            {
                Main(input, maxObj, numObjTotal, subRange, xMin, xMax, yMin, yMax, outputGML, outputOBJ, thread);
            }, inputArgument, maxObjOption, numObjTotalOption, rangeOption, xMinOption, xMaxOption, yMinOption, yMaxOption, outputGMLOption, outputOBJOption, threadOption);

            return rootCommand.Invoke(args);
        }

        static void Main(FileInfo input, int maxObj = 30, int numObjTotal = 1082015, bool subRange = false, double xMin = 296179.641, double xMax = 312064.672, double yMin = 45775.873, double yMax = 51791.676, FileInfo? outputGML = null, FileInfo? outputOBJ = null, int thread = 32)
        {
            bool isOutputGML = outputGML != null;
            bool isOutputOBJ = outputOBJ != null;

            ThreadPool.SetMaxThreads(thread, 2);

            XmlReader gmlReader = XmlReader.Create(input.FullName, new XmlReaderSettings() { DtdProcessing = DtdProcessing.Parse });

            int numObjRead = 0;
            int numObjExported = 0;
            object numObjExportedObj = new object();

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
                objVertexFileStream = new FileStream($"{outputOBJ.FullName}.vert", FileMode.Create);
                objFaceFileStream = new FileStream($"{outputOBJ.FullName}.face", FileMode.Create);

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


                    object numPendingWorkObj = new object();
                    int numPendingWork = 0;
                    while (cityModelReader.Read() &&
                        !(cityModelReader.NodeType == XmlNodeType.EndElement && cityModelReader.LocalName == "CityModel" && cityModelReader.NamespaceURI == "http://www.opengis.net/citygml/2.0") &&
                        (numObjExported < maxObj || maxObj < 0))
                    {
                        if (cityModelReader.NodeType == XmlNodeType.Element && cityModelReader.LocalName == "cityObjectMember" && cityModelReader.NamespaceURI == "http://www.opengis.net/citygml/2.0")
                        {
                            // extract object content

                            string memberStr = cityModelReader.ReadOuterXml();
                            lock (numPendingWorkObj)
                                numPendingWork++;
                            ThreadPool.QueueUserWorkItem((obj) =>
                            {
                                XmlReader memberReader = XmlReader.Create(new StringReader(memberStr), null);
                                XPathDocument memberDocument = new XPathDocument(memberReader);
                                XPathNavigator memberNavigator = memberDocument.CreateNavigator();

                                // range validation
                                bool hasBoundary = true;
                                double lx = 0, ly = 0, lz = 0, ux = 0, uy = 0, uz = 0;

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

                                if(!hasBoundary)
                                {
                                    lx = double.MaxValue;
                                    ly = double.MaxValue;
                                    lz = double.MaxValue;

                                    ux = double.MinValue;
                                    uy = double.MaxValue;
                                    uz = double.MaxValue;

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
                                                double y = double.Parse(vals[i+1]);
                                                double z = double.Parse(vals[i+2]);

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
                                    lock (numObjExportedObj)
                                    {
                                        if (!(numObjExported < maxObj || maxObj < 0))
                                        {
                                            return;
                                        }
                                        numObjExported++;
                                    }

                                    // export OBJ
                                    if (isOutputOBJ)
                                    {
                                        XmlReader memberObjBuffer = XmlReader.Create(new StringReader(memberStr), null);
                                        lock (objFaceWriter)
                                        {
                                            objFaceWriter.WriteLine($"o {objID}/{objName}");
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

                                numObjRead++;

                                // progress display

                                if (numObjRead % 1000 == 0)
                                {
                                    if (numObjTotal > 0)
                                    {
                                        double ratio = (numObjRead / (double)numObjTotal);
                                        Console.WriteLine($"[{ratio:P}] {numObjExported} City Object exported");
                                    }
                                    else
                                    {
                                        Console.WriteLine($"[{numObjRead}] {numObjExported} City Object exported");
                                    }

                                }

                                lock (numPendingWorkObj)
                                    numPendingWork--;
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

        }
    }
}