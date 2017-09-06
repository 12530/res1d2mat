using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using DHI.Mike1D.Generic;
using DHI.Mike1D.ResultDataAccess;
using csmatio.io;
using csmatio.types;

namespace res1d2mat
{
    class Program
    {
        static void Main(string[] args)
        {
            //if (args.Length < 1)
            //{
            //    throw new Exception("Result file name should be provided!");
            //}
            //string Resultfilename = args[0];
            int evalCount;
            int runId;
            bool success = int.TryParse(args[0], out evalCount);
            if (success)
            {
                //parsing runId
                bool success2 = int.TryParse(args[1], out runId);
                if (success2)
                {
                    var thisPath = System.IO.Directory.GetCurrentDirectory();
                    string Resultfilename = thisPath + "\\AutomaticXSCal\\Model\\run" + runId.ToString() + "\\SonghuaHDv2-" + evalCount.ToString() + ".mhydro - Result Files\\HD_Songhua.res1d";
                    // load a result file
                    IResultData resultData = new ResultData();
                    resultData.Connection = Connection.Create(Resultfilename);
                    Diagnostics resultDiagnostics = new Diagnostics("Diag");
                    resultData.Load(resultDiagnostics);
                    if (resultDiagnostics.ErrorCountRecursive > 0) //Report error messages if errors found in result file
                    {
                        throw new Exception("Result file could not be loaded.");
                    }

                    IRes1DReaches reaches = resultData.Reaches; //Load reaches data. A reach is a branch, or a part of a branch between two branch connections.
                    if (reaches.Count == 0) throw new Exception("The selected file doesn't contain any branch to export data from. Please select another file."); //Report error message if no branch exists in file (e.g. for a catchment result file)
                    if (reaches[0].DataItems.Count == 0) throw new Exception("The selected file doesn't contain any distributed item on branches and cannot be processed. Please select another file."); //Report error if no result item exists in grid points (e.g. for a result file containing only structure results)

                    int ExportItemIndex = 0;// DataItem = 0, for WaterLevel; DataItem = 1, for Discharge. 
                    string Outputfilename = thisPath + "\\AutomaticXSCal\\Model\\run" + runId.ToString() + "\\ExportMain" + evalCount.ToString() + ".txt";
                    StreamWriter SW = File.CreateText(Outputfilename); //Create the ouptut text file
                    SW.WriteLine(reaches[0].DataItems[ExportItemIndex].Quantity.Description.ToString() + " results (" + reaches[0].DataItems[ExportItemIndex].Quantity.EumQuantity.UnitDescription.ToString() + ") exported from " + Resultfilename + " on " + DateTime.Now); //Write input information (file name, item, unit) and date in the output text file
                    SW.WriteLine("Branch name" + "\t" + "Chainage" + "\t" + "X coordinate" + "\t" + "Y coordinate" + "\t" + "Min. value" + "\t" + "Max. value" + "\t" + "Mark 1 level" + "\t" + "Mark 2 level" + "\t" + "Mark 3 level"); //Write header to the output file


                    // Loop over all reaches
                    for (int j = 0; j < reaches.Count; j++)
                    {
                        IRes1DReach reach = reaches[j]; //Load reach number j
                        IDataItem ResultDataItem = reach.DataItems[ExportItemIndex]; //Load data item number k (for example, number 0 for water level, in a standard HD result file)
                        int[] indexList = ResultDataItem.IndexList; //Load the list of indexes for the current reach and the current data item. Each calculation point has its own index in the reach 

                        //export water level 
                        //StreamWriter SWWL = File.CreateText("WaterLevel.txt");
                        //using csmatio 
                        string matFpath = thisPath + "\\AutomaticXSCal\\Model\\run" + runId.ToString() + "\\WaterLevelTS" + evalCount.ToString() + ".mat";
                        int[] dim_WL = new int[] { 2922, ResultDataItem.NumberOfElements };// 2922 is the time series length at daily step
                        MLDouble mlWL = new MLDouble("WLsim", dim_WL);

                        // Loop over all calculation points from the current reach
                        for (int i = 0; i < ResultDataItem.NumberOfElements; i++)
                        {
                            if (indexList != null) //Check if there is a calculation point
                            {
                                float[] TSDataForElement = ResultDataItem.CreateTimeSeriesData(i); //Get the time series for calculation point i
                                double MaxDataForElement = TSDataForElement.Max(); //Get the maximum value from this time series
                                double MinDataForElement = TSDataForElement.Min(); //Get the minimum value from this time series
                                // too slow
                                //string wl_all = "";
                                //foreach (double wl in TSDataForElement)
                                //{
                                //    wl_all = wl_all + wl.ToString() + ",";
                                //}
                                //SWWL.WriteLine(wl_all.Substring(0,wl_all.Length - 1));
                                int ielement = 0;
                                foreach (double wl in TSDataForElement)
                                {
                                    mlWL.Set(wl, ielement, i);
                                    ielement++;
                                }

                                int gridPointIndex = ResultDataItem.IndexList[i]; //Get the index of calculation point i
                                IRes1DGridPoint gridPoint = reach.GridPoints[gridPointIndex]; //Load calculation point

                                if (gridPoint is IRes1DHGridPoint) //Processing of h-points
                                {
                                    IRes1DHGridPoint hGridPoint = gridPoint as IRes1DHGridPoint;
                                    IRes1DCrossSection crossSection = hGridPoint.CrossSection;
                                    if (crossSection is IRes1DOpenCrossSection) //Check if calculation point has an open cross section, in which case extra information will be extracted
                                    {
                                        IRes1DOpenCrossSection openXs = crossSection as IRes1DOpenCrossSection;
                                        double M1 = openXs.Points[openXs.LeftLeveeBank].Z; //Get Z elevation for marker 1
                                        double M2 = openXs.Points[openXs.LowestPoint].Z; //Get Z elevation for marker 2
                                        double M3 = openXs.Points[openXs.RightLeveeBank].Z; //Get Z elevation for marker 3
                                        SW.WriteLine(reach.Name + "\t" + hGridPoint.Chainage.ToString() + "\t" + hGridPoint.X.ToString() + "\t" + hGridPoint.Y.ToString() + "\t" + MinDataForElement.ToString() + "\t" + MaxDataForElement.ToString() + "\t" + M1.ToString() + "\t" + M2.ToString() + "\t" + M3.ToString()); //Write all information including marker levels to output file
                                    }
                                    else
                                    {
                                        SW.WriteLine(reach.Name + "\t" + hGridPoint.Chainage.ToString() + "\t" + hGridPoint.X.ToString() + "\t" + hGridPoint.Y.ToString() + "\t" + MinDataForElement.ToString() + "\t" + MaxDataForElement.ToString()); //For other calculation points (without cross sections), write information to output file
                                    }
                                }
                                else if (gridPoint is IRes1DQGridPoint) //Processing of regular Q-points
                                {
                                    IRes1DQGridPoint QGridPoint = gridPoint as IRes1DQGridPoint;
                                    SW.WriteLine(reach.Name + "\t" + QGridPoint.Chainage.ToString() + "\t" + QGridPoint.X.ToString() + "\t" + QGridPoint.Y.ToString() + "\t" + MinDataForElement.ToString() + "\t" + MaxDataForElement.ToString()); //Write information to output file
                                }
                                else if (gridPoint is IRes1DStructureGridPoint) //Processing of structure Q-points
                                {
                                    IRes1DStructureGridPoint QGridPoint = gridPoint as IRes1DStructureGridPoint;
                                    SW.WriteLine(reach.Name + "\t" + QGridPoint.Chainage.ToString() + "\t" + QGridPoint.X.ToString() + "\t" + QGridPoint.Y.ToString() + "\t" + MinDataForElement.ToString() + "\t" + MaxDataForElement.ToString()); //Write information to output file
                                }
                                else
                                {
                                    SW.WriteLine("WARNING: a calculation point with a non-supported type could not be exported."); //Ensure that if a specific type of point is not supported by the current program, a message is returned and the program can proceed with the other points
                                }
                            }
                        }
                        //Save .mat files
                        List<MLArray> mlList = new List<MLArray>();
                        mlList.Add(mlWL);
                        MatFileWriter mfw = new MatFileWriter(matFpath, mlList, false);
                    }
                    SW.Close(); //Release the ouput file
                    resultData.Dispose(); //Release the result file
                }
            }
            
        }
    }
}
