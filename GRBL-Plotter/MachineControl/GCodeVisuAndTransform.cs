﻿/*  GRBL-Plotter. Another GCode sender for GRBL.
    This file is part of the GRBL-Plotter application.
   
    Copyright (C) 2015-2019 Sven Hasemann contact: svenhb@web.de

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/
/*  GCodeVisuAndTransform.cs
    Scaling, Rotation, Remove OffsetXY, Mirror X or Y
    During transformation the drawing path will be generated, because cooridantes are already parsed.
    Return transformed GCode 
*/
/* 2016-09-18 use gcode.frmtNum to control amount of decimal places
 * 2018-04-03 code clean up
 * 2019-01-12 add some comments to getGCodeLine
 * 2019-01-24 change lines 338, 345, 356, 363 to get xyz dimensions correctly
 * 2019-01-28 3 digits for dimension in getMinMaxString()
 * 2019-02-06 add selection high-light
 * 2019-02-09 outsourcing of code for parsing
 * 2019-02-27 createGCodeProg add output for A,B,C,U,V,W axis
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing.Drawing2D;
using System.Drawing;
using System.Windows.Forms;

namespace GRBL_Plotter
{
    public class GCodeVisuAndTransform
    {   public enum translate { None, ScaleX, ScaleY, Offset1, Offset2, Offset3, Offset4, Offset5, Offset6, Offset7, Offset8, Offset9, MirrorX, MirrorY };
        public Dimensions xyzSize = new Dimensions();
        public static drawingProperties drawingSize = new drawingProperties();
        public static GraphicsPath pathPenUp = new GraphicsPath();
        public static GraphicsPath pathPenDown = new GraphicsPath();
        public static GraphicsPath pathRuler = new GraphicsPath();
        public static GraphicsPath pathTool = new GraphicsPath();
        public static GraphicsPath pathMarker = new GraphicsPath();
        public static GraphicsPath pathHeightMap = new GraphicsPath();
        public static GraphicsPath pathMachineLimit = new GraphicsPath();
        public static GraphicsPath pathToolTable = new GraphicsPath();
        public static GraphicsPath pathBackground = new GraphicsPath();
        public static GraphicsPath pathMarkSelection = new GraphicsPath();
        public static GraphicsPath path = pathPenUp;

        public bool containsG2G3Command()
        { return modal.containsG2G3; }
        public bool containsG91Command()
        { return modal.containsG91; }

        private xyPoint origWCOLandMark = new xyPoint();
        private List<coordByLine> coordListLandMark = new List<coordByLine>();
        /// <summary>
        /// copy actual gcode-pathPenDown to background path with machine coordinates
        /// </summary>
        public void setPathAsLandMark(bool clear=false)
        {   if (clear)
            {   pathBackground.Reset();
                coordListLandMark.Clear();
                return;
            }
            pathBackground = (GraphicsPath)pathPenDown.Clone();
            coordListLandMark = new List<coordByLine>();
            foreach (coordByLine gcline in coordList)        // copy coordList and add WCO
            {   coordListLandMark.Add(new coordByLine(0, -1, gcline.actualPos + (xyPoint)grbl.posWCO));
            }
            origWCOLandMark = (xyPoint)grbl.posWCO;
        }
        /// <summary>
        /// translate background path with machine coordinates to take account of changed WCO
        /// </summary>
        public void updatePathPositions()
        {
            Matrix matrix = new Matrix();
            matrix.Translate((float)(origWCOLandMark.X - grbl.posWCO.X), (float)(origWCOLandMark.Y - grbl.posWCO.Y));
            pathBackground.Transform(matrix);
            origWCOLandMark = (xyPoint)grbl.posWCO;

            matrix.Reset();
            matrix.Translate((float)(origWCOMachineLimit.X - grbl.posWCO.X), (float)(origWCOMachineLimit.Y - grbl.posWCO.Y));
            pathMachineLimit.Transform(matrix);
            pathToolTable.Transform(matrix);
            origWCOMachineLimit = (xyPoint)grbl.posWCO;
        }

        private xyPoint origWCOMachineLimit = new xyPoint();
        /// <summary>
        /// create paths with machine limits and tool positions in machine coordinates
        /// </summary>
        public void drawMachineLimit(toolPos[] toolTable)
        {
            float offsetX =  (float)grbl.posWCO.X;   // (float)machinePos.X - (float)grbl.posWork.X;// toolPos.X;
            float offsetY =  (float)grbl.posWCO.Y;   // (float)machinePos.Y- (float)grbl.posWork.Y;//toolPos.Y;
            float x1 = (float)Properties.Settings.Default.machineLimitsHomeX - offsetX;
            float y1 = (float)Properties.Settings.Default.machineLimitsHomeY - offsetY;
            float rx = (float)Properties.Settings.Default.machineLimitsRangeX;
            float ry = (float)Properties.Settings.Default.machineLimitsRangeY;
            float extend = 2 * rx;
            RectangleF pathRect1 = new RectangleF(x1, y1, rx, ry);
            RectangleF pathRect2 = new RectangleF(x1- extend, y1- extend, rx + 2 * extend, ry + 2 * extend); //(float.MinValue, float.MinValue, float.MaxValue, float.MaxValue);
            pathMachineLimit.Reset();
            pathMachineLimit.StartFigure();
            pathMachineLimit.AddRectangle(pathRect1);
            pathMachineLimit.AddRectangle(pathRect2);
            pathToolTable.Reset();
            if ((toolTable != null) && (toolTable.Length >= 1))
            {
                Matrix matrix = new Matrix();
                matrix.Scale(1, -1);
                float wx, wy;
                foreach (toolPos tpos in toolTable)
                {
                    wx = tpos.X - offsetX; wy = tpos.Y - offsetY;
                    if ((tpos.name.Length > 1) && (tpos.toolnr >= 0))
                    {
                        pathToolTable.StartFigure();
                        pathToolTable.AddEllipse(wx - 4, wy - 4, 8, 8);
                        pathToolTable.Transform(matrix);
                        pathToolTable.AddString(tpos.toolnr.ToString() + ") " + tpos.name, new FontFamily("Arial"), (int)FontStyle.Regular, 4, new Point((int)wx - 12, -(int)wy + 4), StringFormat.GenericDefault);
                        pathToolTable.Transform(matrix);
                    }
                }
            }
            origWCOMachineLimit = (xyPoint)grbl.posWCO;
        }

        private static float maxStep = 100;
        /// <summary>
        /// create height map path in work coordinates
        /// </summary>
        public void drawHeightMap(HeightMap Map)
        {   pathHeightMap.Reset();
            Vector2 tmp;
            int x = 0, y = 0;
            for (y = 0; y < Map.SizeY; y++)
            {   tmp = Map.GetCoordinates(x, y);
                pathHeightMap.StartFigure();
                pathHeightMap.AddLine((float)Map.Min.X, (float)tmp.Y, (float)Map.Max.X, (float)tmp.Y);
            }
            for (x = 0; x < Map.SizeX; x++)
            {
                tmp = Map.GetCoordinates(x, Map.SizeY-1);
                pathHeightMap.StartFigure();
                pathHeightMap.AddLine((float)tmp.X, (float)Map.Min.Y, (float)tmp.X, (float)Map.Max.Y);
            }
            tmp = Map.GetCoordinates(0, 0);
            xyzSize.setDimensionXY(tmp.X, tmp.Y);
            tmp = Map.GetCoordinates(Map.SizeX, Map.SizeY);
            xyzSize.setDimensionXY(tmp.X, tmp.Y);
        }

        /// <summary>
        /// apply new z-value to all gcode coordinates
        /// </summary>
        public string applyHeightMap(IList<string> oldCode, HeightMap Map)
        {
            maxStep = (float)Map.GridX;
            getGCodeLines(oldCode,true);                // read gcode and process subroutines
            IList<string> tmp=createGCodeProg(true,false,false).Split('\r').ToList();      // split lines and arcs createGCodeProg(bool replaceG23, bool applyNewZ, bool removeZ, HeightMap Map=null)
            getGCodeLines(tmp, false);                  // reload code
            return createGCodeProg(false, true, false, Map);        // apply new Z-value;
        }

        /// <summary>
        /// undo height map (reload saved backup)
        /// </summary>
        public static void clearHeightMap()
        {   pathHeightMap.Reset(); }

        // analyse each GCode line and track actual position and modes for each code line
        private List<gcodeByLine> gcodeList;        // keep original program
        private List<coordByLine> coordList;        // get all coordinates (also subroutines)

        private gcodeByLine oldLine = new gcodeByLine();    // actual parsed line
        private gcodeByLine newLine = new gcodeByLine();    // last parsed line

        private modalGroup modal = new modalGroup();        // keep modal states and helper variables

        /// <summary>
        /// Entrypoint for generating drawing path from given gcode
        /// </summary>
        public void getGCodeLines(IList<string> oldCode, bool processSubs=false)
        {
            string[] GCode = oldCode.ToArray<string>();
            string singleLine;
            modal = new modalGroup();               // clear
            gcodeList = new List<gcodeByLine>();    //.Clear();
            coordList = new List<coordByLine>();    //.Clear();
            clearDrawingnPath();                    // reset path, dimensions

            oldLine.resetAll(grbl.posWork);         // reset coordinates and parser modes, set initial pos
            newLine.resetAll();                     // reset coordinates and parser modes
            bool programEnd = false;
            figureCount = 0;                        // will be inc. in createDrawingPathFromGCode

            for (int lineNr = 0; lineNr < GCode.Length; lineNr++)   // go through all gcode lines
            {
                modal.resetSubroutine();                            // reset m, p, o, l Word
                singleLine = GCode[lineNr].ToUpper().Trim();        // get line, remove unneeded chars
                if (singleLine == "")
                    continue;
                if (processSubs && programEnd)
                { singleLine = "( " + singleLine + " )"; }          // don't process subroutine itself when processed

                newLine.parseLine(lineNr, singleLine, ref modal);
                calcAbsPosition(newLine, oldLine);                  // Calc. absolute positions and set object dimension: xyzSize.setDimension

                if ((modal.mWord == 98) && processSubs)
                    newLine.codeLine = "(" + GCode[lineNr] + ")";
                else
                {   if (processSubs && programEnd)
                        newLine.codeLine = "( " + GCode[lineNr] + " )";   // don't process subroutine itself when processed
                    else
                        newLine.codeLine = GCode[lineNr];                 // store original line
                }

                if (!programEnd)
                    createDrawingPathFromGCode(newLine, oldLine);       // add data to drawing path
                               
                oldLine = new gcodeByLine(newLine);                     // get copy of newLine      
                gcodeList.Add(new gcodeByLine(newLine));                // add parsed line to list
                coordList.Add(new coordByLine(lineNr, newLine.figureNumber, (xyPoint)newLine.actualPos));

                if ((modal.mWord == 30) || (modal.mWord == 2)) { programEnd = true; }
                if (modal.mWord == 98)
                {   if (lastSubroutine[0] == modal.pWord)
                        addSubroutine(GCode, lastSubroutine[1], lastSubroutine[2], modal.lWord, processSubs);
                    else
                        findAddSubroutine(modal.pWord, GCode, modal.lWord, processSubs);      // scan complete GCode for matching O-word
                }            
            }
        }
        /// <summary>
        /// Find and add subroutine within given gcode
        /// </summary>
        private string findAddSubroutine(int foundP, string[] GCode, int repeat, bool processSubs)
        {
            modalGroup tmp = new modalGroup();                      // just temporary use
            gcodeByLine tmpLine = new gcodeByLine();                // just temporary use
            int subStart=0, subEnd=0;
            bool foundO = false;
            for (int lineNr = 0; lineNr < GCode.Length; lineNr++)   // go through GCode lines
            {   tmpLine.parseLine(lineNr, GCode[lineNr], ref tmp);       // parse line
                if (tmp.oWord == foundP)                            // subroutine ID found?
                {   if (!foundO)
                    {   subStart = lineNr;       
                        foundO = true;
                    }
                    else
                    {   if (tmp.mWord == 99)                        // subroutine end found?
                        {   subEnd = lineNr;    
                            break;
                        }
                    }
                }
            }
            if ((subStart > 0) && (subEnd > subStart))
            {   addSubroutine(GCode, subStart, subEnd, repeat, processSubs);    // process subroutine
                lastSubroutine[0] = foundP;
                lastSubroutine[1] = subStart;
                lastSubroutine[2] = subEnd;
            }
            return String.Format("Start:{0} EndX:{1} ", subStart, subEnd);      
        }
        private int[] lastSubroutine = new int[] { 0, 0, 0 };

        /// <summary>
        /// process subroutines
        /// </summary>
        private void addSubroutine(string[] GCode, int start, int stop, int repeat, bool processSubs)
        {   bool showPath = true;
            for (int loop = 0; loop < repeat; loop++)
            {   for (int subLineNr = start + 1; subLineNr < stop; subLineNr++)      // go through real line numbers and parse sub-code
                {   if (GCode[subLineNr].IndexOf("%START_HIDECODE") >= 0) { showPath = false; }
                    if (GCode[subLineNr].IndexOf("%STOP_HIDECODE") >= 0) { showPath = true; }

                    newLine.parseLine(subLineNr, GCode[subLineNr], ref modal);      // reset coordinates, set lineNumber, parse GCode
                    newLine.isSubroutine = !processSubs;
                    calcAbsPosition(newLine, oldLine);                              // calc abs position

                    if (!showPath) newLine.ismachineCoordG53 = true;

                    if (processSubs)
                        gcodeList.Add(new gcodeByLine(newLine));      // add parsed line to list
                    if (!newLine.ismachineCoordG53)
                    { coordList.Add(new coordByLine(subLineNr, newLine.figureNumber, (xyPoint)newLine.actualPos));
                        if (((newLine.motionMode > 0) || (newLine.z != null)) && !((newLine.x == grbl.posWork.X) && (newLine.y == grbl.posWork.Y)))
                            xyzSize.setDimensionXYZ(newLine.actualPos.X, newLine.actualPos.Y, newLine.actualPos.Z);             // calculate max dimensions
                    }                                                                                                       // add data to drawing path
                    if (showPath)
                        createDrawingPathFromGCode(newLine, oldLine);
                    oldLine = new gcodeByLine(newLine);   // get copy of newLine                         
                }
            }
        }


        /// <summary>
        /// Calc. absolute positions and set object dimension: xyzSize.setDimension
        /// </summary>
        private void calcAbsPosition(gcodeByLine newLine, gcodeByLine oldLine)
        {
            if (!newLine.ismachineCoordG53)         // only use world coordinates
            {   if ((newLine.motionMode >= 1) && (oldLine.motionMode == 0))     // take account of last G0 move
                {   xyzSize.setDimensionX(oldLine.actualPos.X);
                    xyzSize.setDimensionY(oldLine.actualPos.Y);
                }
                if (newLine.x != null)
                {   if (newLine.isdistanceModeG90)  // absolute move
                    {   newLine.actualPos.X = (double)newLine.x;
                        if(newLine.motionMode >=1 )//if (newLine.actualPos.X != toolPos.X)            // don't add actual tool pos
                        {   xyzSize.setDimensionX(newLine.actualPos.X);
                        }
                    }
                    else
                    {   newLine.actualPos.X = oldLine.actualPos.X + (double)newLine.x;
                        if (newLine.motionMode >= 1)//if (newLine.actualPos.X != toolPos.X)            // don't add actual tool pos
                        {   xyzSize.setDimensionX(newLine.actualPos.X);// - toolPosX);
                        }
                    }
                }
                else
                    newLine.actualPos.X = oldLine.actualPos.X;

                if (newLine.y != null)
                {   if (newLine.isdistanceModeG90)
                    {   newLine.actualPos.Y = (double)newLine.y;
                        if (newLine.motionMode >= 1)//if (newLine.actualPos.Y != toolPos.Y)            // don't add actual tool pos
                        {   xyzSize.setDimensionY(newLine.actualPos.Y);
                        }
                    }
                    else
                    {   newLine.actualPos.Y = oldLine.actualPos.Y + (double)newLine.y;
                        if (newLine.motionMode >= 1)//if (newLine.actualPos.Y != toolPos.Y)            // don't add actual tool pos
                        {   xyzSize.setDimensionY(newLine.actualPos.Y);// - toolPosY);
                        }
                    }
                }
                else
                    newLine.actualPos.Y = oldLine.actualPos.Y;

                if (newLine.z != null)
                {   if (newLine.isdistanceModeG90)
                    {   newLine.actualPos.Z = (double)newLine.z;
                        if (newLine.actualPos.Z != grbl.posWork.Z)            // don't add actual tool pos
                            xyzSize.setDimensionZ(newLine.actualPos.Z); // removed - toolPosZ
                    }
                    else
                    {   newLine.actualPos.Z = oldLine.actualPos.Z + (double)newLine.z;
                        if (newLine.actualPos.Z != grbl.posWork.Z)            // don't add actual tool pos
                            xyzSize.setDimensionZ(newLine.actualPos.Z);// - toolPosZ);
                    }
                }
                else
                    newLine.actualPos.Z = oldLine.actualPos.Z;
            }
        }

        /// <summary>
        /// set marker into drawing on xy-position of desired line
        /// </summary>
        public void setPosMarkerLine(int line, bool markFigure=true)
        {
#if (debuginfo)
            log.Add("GCodeVisu setPosMarkerLine line: " + line.ToString());
#endif
            int figureNr;
            try
            {
                if (line < coordList.Count)
                {
                    if (line == coordList[line].lineNumber)
                    {
                        grbl.posMarker = (xyPoint)coordList[line].actualPos;
                        createMarkerPath();
                        figureNr = coordList[line].figureNumber;
                        if ((figureNr != lastFigureNumber) && (markFigure))
                            markSelectedFigure(figureNr);
                        lastFigureNumber = figureNr;
                    }
                    else
                    {
                        foreach (coordByLine gcline in coordList)
                        {
                            if (line == gcline.lineNumber)
                            {
                                grbl.posMarker = (xyPoint)gcline.actualPos;
                                createMarkerPath();
                                figureNr = coordList[line].figureNumber;
                                if ((figureNr != lastFigureNumber) && (markFigure))
                                    markSelectedFigure(figureNr);
                                lastFigureNumber = figureNr;

                                break;
                            }
                        }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// find gcode line with xy-coordinates near by given coordinates
        /// </summary>
        public int setPosMarkerNearBy(xyPoint pos)
        {   List<coordByLine> tmpList = new List<coordByLine>();     // get all coordinates (also subroutines)
            int figureNr;
            foreach (coordByLine gcline in coordList)
            {   gcline.calcDistance(pos);       // calculate distance work coordinates
                tmpList.Add(gcline);            // add to new list

            }
            if (Properties.Settings.Default.backgroundShow && (coordListLandMark.Count > 1))
            {   foreach (coordByLine gcline in coordListLandMark)
                {   gcline.calcDistance(pos+(xyPoint)grbl.posWCO);      // calculate distance machine coordinates
                    tmpList.Add(new coordByLine(0, gcline.figureNumber, gcline.actualPos - (xyPoint)grbl.posWCO, gcline.distance)); // add as work coord.
                }
            }
            int line = 0;
            List<coordByLine> SortedList = tmpList.OrderBy(o => o.distance).ToList();
            grbl.posMarker = (xyPoint)SortedList[line].actualPos;
            figureNr = SortedList[line].figureNumber;
#if (debuginfo)
            log.Add("GCodeVisu setPosMarkerNearBy found figure: " + figureNr.ToString() + " last: "+ lastFigureNumber.ToString());
#endif
            createMarkerPath();
            if (figureNr != lastFigureNumber)
                markSelectedFigure(figureNr);
            lastFigureNumber = figureNr;

            return SortedList[line].lineNumber;
        }
        private int lastFigureNumber = -1;

        /// <summary>
        /// return GCode lineNr of first point in selected path (figure)
        /// </summary>
        public int getLineOfFirstPointInFigure()
        {   if (lastFigureNumber < 0)
                return -1;
            foreach (coordByLine gcline in coordList)
            {   if (gcline.figureNumber == lastFigureNumber)
                    return gcline.lineNumber;
            }
            return -1;
        }

        /// <summary>
        /// return GCode lineNr of last point in selected path (figure)
        /// </summary>
        public int getLineOfEndPointInFigure(int start=0)
        {   if (start < 0)
                return -1;
            int figNr = coordList[start].figureNumber;
            for (int i=start; i < coordList.Count(); i++)
            {
                if (coordList[i].figureNumber != figNr)
                    return coordList[i].lineNumber;
            }
            return -1;
        }

        /// <summary>
        /// create path of selected figure
        /// </summary>
        public void markSelectedFigure(int fnr)
        {
#if (debuginfo)
            log.Add("GCodeVisu markSelectedFigure fnr: " + fnr.ToString());
#endif
            if (fnr <= 0)
            {   pathMarkSelection.Reset();
                return;
            }
            GraphicsPathIterator myPathIterator = new GraphicsPathIterator(pathPenDown);
            bool myIsClosed;
            myPathIterator.Rewind();
            while (myPathIterator.NextSubpath(pathMarkSelection, out myIsClosed) > 0)
            {   if (fnr-- <= 1) break; }
        }

        public string transformGCodeMirror(translate shiftToZero = translate.MirrorX)
        {
            if (gcodeList == null) return "";
            double oldmaxx = xyzSize.maxx;
            double oldmaxy = xyzSize.maxy;
            oldLine.resetAll(grbl.posWork);         // reset coordinates and parser modes
            clearDrawingnPath();                    // reset path, dimensions

            foreach (gcodeByLine gcline in gcodeList)
            {
                if (!gcline.ismachineCoordG53)
                {
                    // switch circle direction
                    if ((shiftToZero == translate.MirrorX) || (shiftToZero == translate.MirrorY))           // mirror xy 
                    {
                        if (gcline.motionMode == 2) { gcline.motionMode = 3; }
                        else if (gcline.motionMode == 3) { gcline.motionMode = 2; }
                    }
                    if (shiftToZero == translate.MirrorX)           // mirror x
                    {
                        if (gcline.x != null)
                        {
                            if (gcline.isdistanceModeG90)
                                gcline.x = oldmaxx - gcline.x;
                            else
                                gcline.x = -gcline.x;
                        }
                        gcline.i = -gcline.i;
                    }
                    if (shiftToZero == translate.MirrorY)           // mirror y
                    {
                        if (gcline.y != null)
                        {
                            if (gcline.isdistanceModeG90)
                                gcline.y = oldmaxy - gcline.y;
                            else
                                gcline.y = -gcline.y;
                        }
                        gcline.j = -gcline.j;
                    }

                    calcAbsPosition(gcline, oldLine);
                    oldLine = new gcodeByLine(gcline);   // get copy of newLine
                }
            }
            return createGCodeProg(false,false,false);  // createGCodeProg(bool replaceG23, bool applyNewZ, bool removeZ, HeightMap Map=null)
        }
        /// <summary>
        /// rotate and scale arround offset
        /// </summary>
        public string transformGCodeRotate(double angle, double scale, xyPoint offset)
        {   if (gcodeList == null) return "";

            double? newvalx, newvaly, newvali, newvalj;
            oldLine.resetAll(grbl.posWork);         // reset coordinates and parser modes
            clearDrawingnPath();                    // reset path, dimensions
            foreach (gcodeByLine gcline in gcodeList)
            {
                if (!gcline.ismachineCoordG53)
                {
                    if ((gcline.x != null) || (gcline.y != null))
                    {
                        if (gcline.isdistanceModeG90)
                        {
                            newvalx = (gcline.actualPos.X - offset.X) * Math.Cos(angle * Math.PI / 180) - (gcline.actualPos.Y - offset.Y) * Math.Sin(angle * Math.PI / 180);
                            newvaly = (gcline.actualPos.X - offset.X) * Math.Sin(angle * Math.PI / 180) + (gcline.actualPos.Y - offset.Y) * Math.Cos(angle * Math.PI / 180);
                        }
                        else
                        {
                            if (gcline.x == null) { gcline.x = 0; }
                            if (gcline.y == null) { gcline.y = 0; }
                            newvalx = (gcline.x - offset.X) * Math.Cos(angle * Math.PI / 180) - (gcline.y - offset.Y) * Math.Sin(angle * Math.PI / 180);
                            newvaly = (gcline.x - offset.X) * Math.Sin(angle * Math.PI / 180) + (gcline.y - offset.Y) * Math.Cos(angle * Math.PI / 180);
                        }
                        gcline.x = (newvalx * scale) + offset.X;
                        gcline.y = (newvaly * scale) + offset.Y;
                    }
                    if ((gcline.i != null) || (gcline.j != null))
                    {
                        newvali = (double)gcline.i * Math.Cos(angle * Math.PI / 180) - (double)gcline.j * Math.Sin(angle * Math.PI / 180);
                        newvalj = (double)gcline.i * Math.Sin(angle * Math.PI / 180) + (double)gcline.j * Math.Cos(angle * Math.PI / 180);
                        gcline.i = newvali * scale;
                        gcline.j = newvalj * scale;
                    }

                    calcAbsPosition(gcline, oldLine);
                    oldLine = new gcodeByLine(gcline);   // get copy of newLine
                }
            }
            return createGCodeProg(false,false, false); // createGCodeProg(bool replaceG23, bool applyNewZ, bool removeZ, HeightMap Map=null)
        }
        /// <summary>
        /// scale x and y seperatly in %
        /// </summary>
        public string transformGCodeScale(double scaleX, double scaleY)
        {
            if (gcodeList == null) return "";
            double factor_x = scaleX / 100;
            double factor_y = scaleY / 100;
            oldLine.resetAll(grbl.posWork);         // reset coordinates and parser modes
            clearDrawingnPath();                    // reset path, dimensions
            foreach (gcodeByLine gcline in gcodeList)
            {
                if (!gcline.ismachineCoordG53)
                {
                    if (gcline.x != null)
                        gcline.x = gcline.x * factor_x;
                    if (gcline.y != null)
                        gcline.y = gcline.y * factor_y;
                    if (gcline.i != null)
                        gcline.i = gcline.i * factor_x;
                    if (gcline.j != null)
                        gcline.j = gcline.j * factor_y;

                    calcAbsPosition(gcline, oldLine);
                    oldLine = new gcodeByLine(gcline);   // get copy of newLine
                }
            }
            return createGCodeProg(false, false, false);        // createGCodeProg(bool replaceG23, bool applyNewZ, bool removeZ, HeightMap Map=null)
        }
        public string transformGCodeOffset(double x, double y, translate shiftToZero)
        {
            if (gcodeList == null) return "";
            double offsetX = 0;
            double offsetY = 0;
            bool offsetApplied = false;
            bool noInsertNeeded = false;
            oldLine.resetAll(grbl.posWork);         // reset coordinates and parser modes
            if (shiftToZero == translate.Offset1) { offsetX = x + xyzSize.minx;                     offsetY = y + xyzSize.miny + xyzSize.dimy; }
            if (shiftToZero == translate.Offset2) { offsetX = x + xyzSize.minx + xyzSize.dimx / 2;  offsetY = y + xyzSize.miny + xyzSize.dimy; }
            if (shiftToZero == translate.Offset3) { offsetX = x + xyzSize.minx + xyzSize.dimx;      offsetY = y + xyzSize.miny + xyzSize.dimy; }
            if (shiftToZero == translate.Offset4) { offsetX = x + xyzSize.minx;                     offsetY = y + xyzSize.miny + xyzSize.dimy / 2; }
            if (shiftToZero == translate.Offset5) { offsetX = x + xyzSize.minx + xyzSize.dimx / 2;  offsetY = y + xyzSize.miny + xyzSize.dimy / 2; }
            if (shiftToZero == translate.Offset6) { offsetX = x + xyzSize.minx + xyzSize.dimx;      offsetY = y + xyzSize.miny + xyzSize.dimy / 2; }
            if (shiftToZero == translate.Offset7) { offsetX = x + xyzSize.minx;                     offsetY = y + xyzSize.miny; }
            if (shiftToZero == translate.Offset8) { offsetX = x + xyzSize.minx + xyzSize.dimx / 2;  offsetY = y + xyzSize.miny; }
            if (shiftToZero == translate.Offset9) { offsetX = x + xyzSize.minx + xyzSize.dimx;      offsetY = y + xyzSize.miny; }

            if (modal.containsG91)    // relative move: insert rapid movement before pen down, to be able applying offset
            {
                newLine.resetAll();
                int i,k;
                bool foundG91 = false;
                for (i = 0; i < gcodeList.Count; i++)       // find first relative move
                {   if ((!gcodeList[i].isdistanceModeG90) && (!gcodeList[i].isSubroutine) && (gcodeList[i].motionMode == 0) && (gcodeList[i].z != null))       
                    { foundG91 = true; break; }
                }
                if (foundG91)
                {
                    for (k = i + 1; k < gcodeList.Count; k++)   // find G0 x y
                    {
                        if ((gcodeList[k].motionMode == 0) && (gcodeList[k].x != null) && (gcodeList[k].y != null))
                        { noInsertNeeded = true; break; }
                        if (gcodeList[k].motionMode > 0)
                            break;
                    }
                    if (!noInsertNeeded)
                    {
                        if ((gcodeList[i + 1].motionMode != 0) || ((gcodeList[i + 1].motionMode == 0) && ((gcodeList[i + 1].x == null) || (gcodeList[i + 1].y == null))))
                        {
                            if ((!noInsertNeeded) && (!gcodeList[i + 1].ismachineCoordG53))
                            {   //getGCodeLine("G0 X0 Y0 (Insert offset movement)", newLine);                   // parse line, fill up newLine.xyz and actualM,P,O
                                modalGroup tmp = new modalGroup();
                                newLine.parseLine(i, "G0 X0 Y0 (Insert offset movement)", ref tmp);
                                gcodeList.Insert(i + 1, newLine);
                            }
                        }
                    }
                }
            }
            bool hide_code = false; ;
            foreach (gcodeByLine gcline in gcodeList)
            {
                if (gcline.codeLine.IndexOf("%START_HIDECODE") >= 0) { hide_code  = true; }
                if (gcline.codeLine.IndexOf("%STOP_HIDECODE") >= 0)  { hide_code = false; }
                if ((!hide_code) && (!gcline.isSubroutine) && (!gcline.ismachineCoordG53) && (gcline.codeLine.IndexOf("(Setup - GCode") < 1)) // ignore coordinates from setup footer
                {
                    if (gcline.isdistanceModeG90)           // absolute move: apply offset to any XY position
                    {
                        if (gcline.x != null)
                            gcline.x = gcline.x - offsetX;      // apply offset
                        if (gcline.y != null)
                            gcline.y = gcline.y - offsetY;      // apply offset
                    }
                    else
                    {   if (!offsetApplied)                 // relative move: apply offset only once
                        {   if (gcline.motionMode == 0)
                            {
                                    gcline.x = gcline.x - offsetX;
                                    gcline.y = gcline.y - offsetY;
                                    if ((gcline.x != null) && (gcline.y != null))
                                        offsetApplied = true;
                            }
                        }
                    }
                    calcAbsPosition(gcline, oldLine);
                    oldLine = new gcodeByLine(gcline);   // get copy of newLine
                }
            }
            return createGCodeProg(false, false, false);   // createGCodeProg(bool replaceG23, bool applyNewZ, bool removeZ, HeightMap Map=null)
        }

        public string replaceG23()
        {   return createGCodeProg(true,false,false); }   // createGCodeProg(bool replaceG23, bool applyNewZ, bool removeZ, HeightMap Map=null)

        public string removeZ()
        { return createGCodeProg(false, false, true); }   // createGCodeProg(bool replaceG23, bool applyNewZ, bool removeZ, HeightMap Map=null)

        /// <summary>
        /// Generate GCode from given coordinates in GCodeList
        /// only replace lines with coordinate information
        /// </summary>
        private string createGCodeProg(bool replaceG23, bool applyNewZ, bool removeZ, HeightMap Map=null)
        {
            if (gcodeList == null) return "";
            StringBuilder newCode = new StringBuilder();
            StringBuilder tmpCode = new StringBuilder();
            //string infoCode;
            bool getCoordinateXY, getCoordinateZ;
            double feedRate = 0;
            double spindleSpeed=0;
            double lastActualX = 0, lastActualY = 0,i,j;
            double newZ = 0;
            int lastMotionMode = 0;
            xyzSize.resetDimension();
            bool hide_code = false;
            for (int iCode=0; iCode < gcodeList.Count; iCode++)     // go through all code lines
            {   gcodeByLine gcline = gcodeList[iCode];
                tmpCode.Clear();
                getCoordinateXY = false;
                getCoordinateZ = false;
                if (gcline.codeLine.Length == 0)
                    continue;

                if (gcline.codeLine.IndexOf("%START_HIDECODE") >= 0) { hide_code = true; }
                if (gcline.codeLine.IndexOf("%STOP_HIDECODE") >= 0) { hide_code = false; }

                if ((!hide_code) && (replaceG23))                   // replace circles
                {
                    gcode.lastx = (float)lastActualX;
                    gcode.lasty = (float)lastActualY;
                    gcode.gcodeXYFeed = gcline.feedRate;
                    if (gcline.isdistanceModeG90)
                        gcode.gcodeRelative = false;
                    else
                        gcode.gcodeRelative = true;
                    if (gcline.motionMode > 1)
                    {
                        i = (double)((gcline.i != null) ? gcline.i : 0.0);
                        j = (double)((gcline.j != null) ? gcline.j : 0.0);
                        gcode.splitArc(newCode, gcline.motionMode, (float)lastActualX, (float)lastActualY, (float)gcline.actualPos.X, (float)gcline.actualPos.Y, (float)i, (float)j, true, gcline.codeLine);
                    }
                    else if (gcline.motionMode == 1)
                    {   if ((gcline.x != null) || (gcline.y != null))
                            gcode.splitLine(newCode, gcline.motionMode, (float)lastActualX, (float)lastActualY, (float)gcline.actualPos.X, (float)gcline.actualPos.Y, maxStep, true, gcline.codeLine);
                        else
                        { newCode.AppendLine(gcline.codeLine.Trim('\r', '\n')); }
                    }
                    else
                    { newCode.AppendLine(gcline.codeLine.Trim('\r', '\n')); }

                }
                else
                {
                    if (gcline.x != null)
                    { tmpCode.AppendFormat(" X{0}", gcode.frmtNum((double)gcline.x)); getCoordinateXY = true; }
                    if (gcline.y != null)
                    { tmpCode.AppendFormat(" Y{0}", gcode.frmtNum((double)gcline.y)); getCoordinateXY = true; }
                    if ((getCoordinateXY || (gcline.z != null)) && applyNewZ && (Map != null))  //(gcline.motionMode != 0) &&       if (getCoordinateXY && applyNewZ && (Map != null))
                    {   newZ = Map.InterpolateZ(gcline.actualPos.X, gcline.actualPos.Y);
                        if ((gcline.motionMode != 0) || (newZ > 0))
                            gcline.z = gcline.actualPos.Z + newZ;
                    }

                    if (gcline.z != null)
                    {   if (!removeZ)
                        { tmpCode.AppendFormat(" Z{0}", gcode.frmtNum((double)gcline.z)); }
                        getCoordinateZ = true;
                    }
                    if (gcline.a != null)
                    { tmpCode.AppendFormat(" A{0}", gcode.frmtNum((double)gcline.a)); getCoordinateZ = true; }
                    if (gcline.b != null)
                    { tmpCode.AppendFormat(" B{0}", gcode.frmtNum((double)gcline.b)); getCoordinateZ = true; }
                    if (gcline.c != null)
                    { tmpCode.AppendFormat(" C{0}", gcode.frmtNum((double)gcline.c)); getCoordinateZ = true; }
                    if (gcline.u != null)
                    { tmpCode.AppendFormat(" U{0}", gcode.frmtNum((double)gcline.u)); getCoordinateZ = true; }
                    if (gcline.v != null)
                    { tmpCode.AppendFormat(" V{0}", gcode.frmtNum((double)gcline.v)); getCoordinateZ = true; }
                    if (gcline.w != null)
                    { tmpCode.AppendFormat(" W{0}", gcode.frmtNum((double)gcline.w)); getCoordinateZ = true; }
                    if (gcline.i != null)
                    { tmpCode.AppendFormat(" I{0}", gcode.frmtNum((double)gcline.i)); getCoordinateXY = true; }
                    if (gcline.j != null)
                    { tmpCode.AppendFormat(" J{0}", gcode.frmtNum((double)gcline.j)); getCoordinateXY = true; }
                    if ((getCoordinateXY || getCoordinateZ) && (!gcline.ismachineCoordG53) && (!hide_code))
                    {   if ((gcline.motionMode > 0) && (feedRate != gcline.feedRate) && ((getCoordinateXY && !getCoordinateZ) || (!getCoordinateXY && getCoordinateZ)))
                        { tmpCode.AppendFormat(" F{0,0}", gcline.feedRate); }
                        if (spindleSpeed != gcline.spindleSpeed)
                        { tmpCode.AppendFormat(" S{0,0}", gcline.spindleSpeed); }
                        tmpCode.Replace(',', '.');
                        //infoCode = " ( " + gcline.ismachineCoordG53 +"  "+ gcline.isdistanceModeG90 + " ) ";
                        if (gcline.codeLine.IndexOf("(Setup - GCode") > 1)  // ignore coordinates from setup footer
                            newCode.AppendLine(gcline.codeLine);
                        else
                            newCode.AppendLine("G" + gcode.frmtCode(gcline.motionMode) + tmpCode.ToString());// + infoCode);
                    }
                    else
                    {   newCode.AppendLine(gcline.codeLine.Trim('\r','\n'));
                    }
                    lastMotionMode = gcline.motionMode;
                }
                feedRate = gcline.feedRate;
                spindleSpeed = gcline.spindleSpeed;
                lastActualX = gcline.actualPos.X; lastActualY = gcline.actualPos.Y;

                if ((!hide_code) && (!gcline.ismachineCoordG53) && (gcline.codeLine.IndexOf("(Setup - GCode") < 1)) // ignore coordinates from setup footer
                {
                    if (!((gcline.actualPos.X == grbl.posWork.X) && (gcline.actualPos.Y == grbl.posWork.Y)))            // don't add actual tool pos
                        xyzSize.setDimensionXYZ(gcline.actualPos.X, gcline.actualPos.Y, gcline.actualPos.Z);
                }
                coordList[iCode] = new coordByLine(iCode, gcline.figureNumber, (xyPoint)gcline.actualPos);
            }
            return newCode.ToString().Replace(',','.');
        }

        public GCodeVisuAndTransform()
        {   xyzSize.resetDimension(); }
        private void clearDrawingnPath()
        {   xyzSize.resetDimension();
            pathPenUp.Reset();
            pathPenDown.Reset();
            pathRuler.Reset();
            pathTool.Reset();
            pathMarker.Reset();
            path = pathPenUp;
        }

        // add given coordinates to drawing path
        private int onlyZ = 0;
        private int figureCount = 0;
        /// <summary>
        /// add segement to drawing path 'PenUp' or 'PenDown' from old-xyz to new-xyz
        /// </summary>
        private void createDrawingPathFromGCode(gcodeByLine newL, gcodeByLine oldL)
        {
            bool passLimit = false;
            var pathOld = path;

            if (newL.isSubroutine && (!oldL.isSubroutine))
                markPath(pathPenUp, (float)newL.actualPos.X, (float)newL.actualPos.Y, 2); // 2=rectangle

            if (!newL.ismachineCoordG53)    
            {
                if ((newL.motionMode > 0) && (oldL.motionMode == 0))
                { path = pathPenDown; }
                if ((newL.motionMode == 0) && (oldL.motionMode > 0))
                { path = pathPenUp;   }
                

                if ((path != pathOld))
                {   passLimit = true;
                    path.StartFigure();
                    if (path == pathPenDown)
                    {   figureCount++;
                        oldL.figureNumber = figureCount;
                    }
                }

                if (newL.motionMode == 0 || newL.motionMode == 1)
                {
                    if ((newL.actualPos.X != oldL.actualPos.X) || (newL.actualPos.Y != oldL.actualPos.Y) || (oldL.motionMode == 2 || oldL.motionMode == 3))
                    {
                        path.AddLine((float)oldL.actualPos.X, (float)oldL.actualPos.Y, (float)newL.actualPos.X, (float)newL.actualPos.Y);
                        onlyZ = 0;  // x or y has changed
                    }
                    if (newL.actualPos.Z != oldL.actualPos.Z)  //else
                    { onlyZ++; }

                    // mark Z-only movements - could be drills
                    if ((onlyZ > 1) && (passLimit) && (path == pathPenUp))  // pen moved from -z to +z
                    {
                        float markerSize = 1;
                        if (!Properties.Settings.Default.importUnitmm)
                        { markerSize /= 25.4F; }
                        createMarker(pathPenDown, (float)newL.actualPos.X, (float)newL.actualPos.Y, markerSize, 1, false);  // draw cross
                        createMarker(pathPenUp, (float)newL.actualPos.X, (float)newL.actualPos.Y, markerSize, 4, false);    // draw circle
                        path = pathPenUp;
                        onlyZ = 0;
                        passLimit = false;
                    }
                }
            }
            if ((newL.motionMode == 2 || newL.motionMode == 3) && (newL.i != null || newL.j != null))
            {   if (newL.i == null) { newL.i = 0; }
                if (newL.j == null) { newL.j = 0; }
                double i = (double)newL.i;
                double j = (double)newL.j;
                float radius = (float)Math.Sqrt(i * i + j * j);
                if (radius == 0)               // kleinster Wert > 0
                { radius = 0.0000001f; }

                float x1 = (float)(oldL.actualPos.X + i - radius);
                float y1 = (float)(oldL.actualPos.Y + j - radius);

                coordList.Add(new coordByLine(newL.lineNumber, figureCount, new xyPoint(x1+ radius,y1+ radius)));

                float cos1 = (float)i / radius;
                if (cos1 > 1) cos1 = 1;
                if (cos1 < -1) cos1 = -1;
                float a1 = 180-180*(float)(Math.Acos(cos1)/Math.PI);

                if (j > 0) { a1 = -a1; }
                float cos2 = (float)(oldL.actualPos.X + i - newL.actualPos.X) / radius;
                if (cos2 > 1) cos2 = 1;
                if (cos2 < -1) cos2 = -1;
                float a2 = 180-180*(float)(Math.Acos(cos2)/Math.PI);

                if ((oldL.actualPos.Y + j- newL.actualPos.Y) > 0) { a2 = -a2; }
                float da = -(360 + a1 - a2);
                if (newL.motionMode == 3) { da=-(360 + a2 - a1); }
                if (da > 360) { da -= 360; }
                if (da < -360) { da += 360; }
                if (newL.motionMode == 2)
                {   path.AddArc(x1, y1, 2 * radius, 2 * radius, a1, da);
                    if (!newL.ismachineCoordG53)
                        xyzSize.setDimensionCircle(x1 + radius, y1 + radius, radius, a1, da);        // calculate new dimensions
                }
                else
                {   path.AddArc(x1, y1, 2 * radius, 2 * radius, a1, -da);
                    if (!newL.ismachineCoordG53)
                        xyzSize.setDimensionCircle(x1 + radius, y1 + radius, radius, a1, -da);       // calculate new dimensions
                }
            }
            if (path == pathPenDown)
                newL.figureNumber = figureCount;            
            else
                newL.figureNumber = -1;
        }

        private void markPath(GraphicsPath path, float x, float y, int type)
        {   float markerSize = 1;
            if (!Properties.Settings.Default.importUnitmm)
            { markerSize /= 25.4F; }
            createMarker(path, x, y, markerSize, type, false);    // draw circle
        }

        // setup drawing area 
        public void createImagePath()
        {
#if (debuginfo)
            log.Add("GCodeVisu createImagePath" );
#endif
            double extend = 1.01;                                                       // extend dimension a little bit
            double roundTo = 5;                                                         // round-up dimensions
            if (!Properties.Settings.Default.importUnitmm)
            { roundTo = 0.25; }
            drawingSize.minX = Math.Floor(xyzSize.minx* extend / roundTo)* roundTo;                  // extend dimensions
            if (drawingSize.minX >= 0) { drawingSize.minX = -roundTo; }                                          // be sure to show 0;0 position
            drawingSize.maxX = Math.Ceiling(xyzSize.maxx* extend / roundTo) * roundTo;
            drawingSize.minY = Math.Floor(xyzSize.miny* extend / roundTo) * roundTo;
            if (drawingSize.minY >= 0) { drawingSize.minY = -roundTo; }
            drawingSize.maxY = Math.Ceiling(xyzSize.maxy* extend / roundTo) * roundTo;
            double xRange = (drawingSize.maxX - drawingSize.minX);                                              // calculate new size
            double yRange = (drawingSize.maxY - drawingSize.minY);
            createRuler(drawingSize.maxX, drawingSize.maxY);
            createMarkerPath();
        }

        public void createMarkerPath()
        {
#if (debuginfo)
            log.Add("GCodeVisu createMarkerPath");
#endif
            float msize = (float) Math.Max(xyzSize.dimx, xyzSize.dimy) / 50;
            createMarker(pathTool,   (float)grbl.posWork.X,   (float)grbl.posWork.Y, msize, 2);
            createMarker(pathMarker, (float)grbl.posMarker.X, (float)grbl.posMarker.Y, msize, 3);
        }
        private void createRuler(double maxX, double maxY)
        {
            pathRuler.Reset();
            float unit = 1;
            int divider = 1;
            int divider_long    = 100; //
            int divider_med     = 10;
            int divider_short   = 5;
            int show_short      = 500;
            int show_smallest   = 200;
            float length1 = 1F, length2 = 2F, length3 = 3F, length5 = 5F;
            if (!Properties.Settings.Default.importUnitmm)
            {   divider = 16;
                divider_long    = divider; 
                divider_med     = 8;
                divider_short   = 4;
                show_short      = 20*divider;
                show_smallest   = 6*divider;
                maxX = maxX * divider; // unit;
                maxY = maxY * divider; // unit;
                length1 = 0.05F; length2 = 0.1F; length3 = 0.15F; length5 = 0.25F;
            }
            float x = 0, y = 0;
            for (int i = 0; i < maxX; i++)          // horizontal ruler
            {   pathRuler.StartFigure();
                x = (float)i*unit / (float)divider;
                if (i % divider_short == 0)
                {   if (i % divider_long == 0)
                    { pathRuler.AddLine(x, 0, x, -length5); }  // 100                    
                    else if ((i % divider_med == 0) && (maxX < (2* show_short)))
                    { pathRuler.AddLine(x, 0, x, -length3); }  // 10                  
                    else if (maxX < show_short)
                    { pathRuler.AddLine(x, 0, x, -length2); }  // 5
                }
                else if (maxX < show_smallest)
                { pathRuler.AddLine(x, 0, x, -length1); }  // 1
            }
            for (int i = 0; i < maxY; i++)          // vertical ruler
            {   pathRuler.StartFigure();
                y = (float)i*unit / (float)divider;
                if (i % divider_short == 0)
                {   if (i % divider_long == 0)
                    { pathRuler.AddLine(0, y, -length5, y); } // 100                   
                    else if ((i % divider_med == 0) && (maxY < (2* show_short)))
                    { pathRuler.AddLine(0, y, -length3, y); } // 10           
                    else if (maxY < show_short)
                    { pathRuler.AddLine(0, y, -length2, y); } // 5
                }
                else if (maxY < show_smallest)
                { pathRuler.AddLine(0, y, -length1, y); }     // 1
            }
        }
        private void createMarker(GraphicsPath path, float centerX,float centerY, float dimension,int style,bool rst=true)
        {   if (dimension == 0) { return; }
            if (rst)
                path.Reset();
            if (style == 0)   // horizontal cross
            {
                path.StartFigure(); path.AddLine(centerX , centerY + dimension, centerX , centerY - dimension);
                path.StartFigure(); path.AddLine(centerX + dimension, centerY , centerX - dimension, centerY );
            }
            else if (style == 1)   // diagonal cross
            {
                path.StartFigure(); path.AddLine(centerX - dimension, centerY + dimension, centerX + dimension, centerY - dimension);
                path.StartFigure(); path.AddLine(centerX - dimension, centerY - dimension, centerX + dimension, centerY + dimension);
            }
            else if (style == 2)            // box
            {
                path.StartFigure(); path.AddLine(centerX - dimension, centerY + dimension, centerX + dimension, centerY + dimension);
                path.StartFigure(); path.AddLine(centerX + dimension, centerY + dimension, centerX + dimension, centerY - dimension);
                path.StartFigure(); path.AddLine(centerX + dimension, centerY - dimension, centerX - dimension, centerY - dimension);
                path.StartFigure(); path.AddLine(centerX - dimension, centerY - dimension, centerX - dimension, centerY + dimension);
            }
            else if (style == 3)            // marker
            {
                path.StartFigure(); path.AddLine(centerX, centerY, centerX, centerY - dimension);
                path.StartFigure(); path.AddLine(centerX, centerY - dimension, centerX + dimension, centerY);
                path.StartFigure(); path.AddLine(centerX + dimension, centerY, centerX, centerY + dimension);
                path.StartFigure(); path.AddLine(centerX, centerY + dimension, centerX - dimension, centerY);
                path.StartFigure(); path.AddLine(centerX - dimension, centerY, centerX, centerY - dimension);
                path.StartFigure(); path.AddLine(centerX, centerY - dimension, centerX, centerY);
            }
            else
            {
                path.StartFigure(); path.AddArc(centerX - dimension, centerY - dimension, 2 * dimension, 2 * dimension, 0, 360);
            }
        }
    }

    public struct drawingProperties
    {
        public double minX,minY,maxX,maxY;
        public void drawingProperty()
         { minX = 0;minY = 0;maxX = 0;maxY=0; }
    };

}
