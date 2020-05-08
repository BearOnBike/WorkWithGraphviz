using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Microsoft.Ajax.Utilities;
using Npgsql;
//using QuickGraph.Algorithms;
//using QuickGraph.Graphviz;
//using QuickGraph.Graphviz.Dot;
//using dp=DotParser;//это на F#
//using QuickGraph;
//using QuickGraph.Algorithms.Search;
//using QuickGraph.FST;
using WorkWithGraphViz.utils;
using Graphviz4Net;
using Graphviz4Net.Dot;
using Graphviz4Net.Dot.AntlrParser;
using Graphviz4Net.Graphs;
using NpgsqlTypes;
using WorkWithGraphViz.Models;


//абстракция на графы и взаимодействие с Graphviz
//для визуализации из web морды, установи пакет graphviz 2.38 через установщик .msi и на папку bin ссылку укажи в классе ClsForInteractionWithGraphviz 


namespace WorkWithGraphViz.Controllers
{
    public class GraphController : Controller
    {
        /// <summary>
        /// Распарсивание графа.
        /// </summary>
        /// <param name="dotcodeforparse"> dot code</param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult ParseGraph(string dotcodeforparse)
        {
            try
            {
                if (!String.IsNullOrEmpty(dotcodeforparse))
                {
                   
                    //Graphviz4Net
                    //DotGraphBuilder<>   
                    var parser = AntlrParserAdapter<string>.GetParser();
                    var graph = parser.Parse(dotcodeforparse);//кавычки в названии(это id) вершин не распарсит! в атрибуте label норм. 
                    if (graph != null && graph.AllVertices.Count() > 0)
                    {

                        #region Переменные
                        
                        StatusModel Sm = new StatusModel();
                        
                        const string colorOptional = "skyblue";
                        const string colorMandatory = "grey";
                        const string colorStop = "red";
                        const string Optional = "OPTIONAL";
                        const string Mandatory = "MANDATORY";
                        const string Stop = "STOP-STATUS";


                        #endregion

                        #region Проверки графа

                        
                        //DotVertex<string>

                        var incorrectWithoutAttributeColorListVertices = graph.AllVertices.Where(
                            //нас интересует только статусы которые не имеют атрибутов вообще или color или label в частности.
                            x => x.Attributes.Count == 0 || !x.Attributes.ContainsKey("color") || !x.Attributes.ContainsKey("label")
                        ).ToList();

                        if (incorrectWithoutAttributeColorListVertices.Count > 0)
                        {
                            string st = "";
                            foreach (var inc in incorrectWithoutAttributeColorListVertices)
                            {
                                st += inc.Id + ", ";
                            }
                            if (!String.IsNullOrEmpty(st)) st = st.Remove(st.Length - 2);
                            ViewBag.Error = $"Есть статусы без атрибута color или label:{st}";
                            return PartialView("ParseNotSuccess");
                        }


                        var correctListVertices =
                            graph.AllVertices.Where(
                                x => x.Attributes
                                    //нас интересует только статусы которые имеют атрибут color с определённым цветом
                                    .Where(a => a.Key == "color" && (a.Value == colorOptional || a.Value == colorMandatory || a.Value == colorStop)).Any()
                            ).ToList();


                        #region уникальность названий(это Id вершин, не метка Label) статусов вершин, уже в парсер вшита, но добавим проверку в прописных 

                        var notunique = correctListVertices.Select(x=>x.Id.ToUpper()).GroupBy(x => x)
                            .Where(x => x.Count() > 1)
                            //что бы конкретный статус отобрать selectmany т.к. мы оперируем группами, select обычный вернёт группу статусов 
                            .SelectMany(x => x).Distinct()
                            .ToList();
                        
                        if (notunique.Count() > 0)
                        {
                            string st = "";
                            foreach (var inc in notunique)
                            {
                                st += inc + ", ";
                            }
                            if (!String.IsNullOrEmpty(st)) st = st.Remove(st.Length - 2);
                            ViewBag.Error = $"Есть статусы c неуникальными названиями вершин (не метка label):{st}";
                            return PartialView("ParseNotSuccess");
                        }
                        #endregion


                   
                        var notUniqueLabel = 
                            correctListVertices
                                .Select(x => x.Attributes["label"])
                            .GroupBy(x => x)
                            .Where(x => x.Count() > 1)
                            //что бы конкретный статус отобрать selectmany т.к. мы оперируем группами, select обычный вернёт группу статусов 
                            .SelectMany(x => x).Distinct()
                            .ToList();

                        if (notUniqueLabel.Any())
                        {
                            string st = "";
                            foreach (var inc in notUniqueLabel)
                            {
                                st += inc + ", ";
                            }
                            if (!String.IsNullOrEmpty(st)) st = st.Remove(st.Length - 2);
                            ViewBag.Error = $"Есть статусы c одинаковыми атрибутами Label:{st}";
                            return PartialView("ParseNotSuccess");
                        }


                        var incorrectColorListVertices = graph.AllVertices.Where(
                            x => x.Attributes
                                //нас интересует только статусы которые имеют атрибут color с не нашим цветом
                                .Where(a => a.Key == "color" && a.Value != colorOptional && a.Value != colorMandatory && a.Value != colorStop).Any()
                        ).ToList();

                        if (incorrectColorListVertices.Count > 0)
                        {
                            string st = "";
                            foreach (var inc in incorrectColorListVertices)
                            {
                                st += inc.Id + ", ";
                            }
                            if (!String.IsNullOrEmpty(st)) st = st.Remove(st.Length - 2);
                            ViewBag.Error = $"Есть статусы c атрибутом color не одобренного цвета:{st}";
                            return PartialView("ParseNotSuccess");
                        }


                        //DotEdge<string>

                        //проверить что  все вершины имеют хотя бы одну связь
                        var vertsFromEdges = new List<DotVertex<string>>();//все вершины участвующие в связях
                        foreach (var ve in graph.VerticesEdges)
                        {
                            vertsFromEdges.Add(ve.Source);
                            vertsFromEdges.Add(ve.Destination);
                        }

                        var vertsNotHaveEdjes = graph.AllVertices.Where(v => !vertsFromEdges.Contains(v)).ToList();
                        if (vertsNotHaveEdjes.Any())
                        {
                            string st = "";
                            foreach (var inc in vertsNotHaveEdjes)
                            {
                                st += inc.Id + ", ";
                            }
                            if (!String.IsNullOrEmpty(st)) st = st.Remove(st.Length - 2);
                            ViewBag.Error = $"Есть вершины без связей с другими вершинами:{st}";
                            return PartialView("ParseNotSuccess");
                        }

                        //TODO тут надо циклы проверить поиск в глубину
                        string isThereСycle = DFS(graph);
                        if (!String.IsNullOrEmpty(isThereСycle))
                        {
                            ViewBag.Error = isThereСycle;
                            return PartialView("ParseNotSuccess");
                        }


                        #endregion

                        #region Заполняем статусную модель

                        //заполняем статусы, idшники добавим после загрузки в БД, ТО ЕСТЬ ДОЛЖНО БЫТЬ УНИКАЛЬНОЕ ИМЯ СТАТУСА(IdFromDotVertex), по логике реализованой изначально оно уникально.
                        foreach (var dv in correctListVertices)
                        {
                            foreach (var kvp in dv.Attributes)
                            {
                                if (kvp.Key == "color")
                                {   //тип статуса в зависимости от цвета присваиваем
                                    switch (kvp.Value)
                                    {
                                        case colorOptional:
                                            Sm.Statuses.Add(new Status() { IdFromDotVertex = dv.Id, Name = dv.Attributes["label"], Describe = "*", Type = Optional });
                                            break;
                                        case colorMandatory:
                                            Sm.Statuses.Add(new Status() { IdFromDotVertex = dv.Id, Name = dv.Attributes["label"], Describe = "*", Type = Mandatory });
                                            break;
                                        case colorStop:
                                            Sm.Statuses.Add(new Status() { IdFromDotVertex = dv.Id, Name = dv.Attributes["label"], Describe = "*", Type = Stop });
                                            break;
                                    }
                                }
                            }

                        }

                        //заполняем связи статусов, idшники добавим после загрузки в БД
                        foreach (var ve in graph.VerticesEdges)
                        {
                            Sm.Workflows.Add(new Workflow()
                            {
                                PrevStatus = Sm.Statuses.Single(x => x.IdFromDotVertex == ve.Source.Id),
                                NextStatus = Sm.Statuses.Single(x => x.IdFromDotVertex == ve.Destination.Id)
                            });
                        }

                        #endregion


                        ViewBag.ModelForInsert = Sm;
                        TempData["StatusModel"] = Sm;
                        
                        return PartialView(Sm);
                    }
                    else
                    {
                        ViewBag.Error = "Не удалось распарсить dot код";
                        return PartialView("ParseNotSuccess");
                    }
                }
                else
                {
                    ViewBag.Error = "Пустая строка в dot";
                    return PartialView("ParseNotSuccess");
                }
            }
            catch (Exception e)
            {
                ViewBag.Error = "Поймали исключение:\n" + e.Message + " в методе:" + e.TargetSite + " StackTrace:" + e.StackTrace + "";
                return PartialView("ParseNotSuccess");
            }

            #region поиск dot парсера

            //тут F#
            //dp.GraphData gd = dp.DotParser.parse("");

            #region Quickgraph




            //GraphvizGraph graphviz = new GraphvizGraph();
            ////qg.IEdge<>
            ////qg.AdjacencyGraph ag= 
            ////graphviz.
            //// graphviz.ToDot();
            ////graphviz.ToString();


            //var g = new AdjacencyGraph<string, TaggedEdge<string, string>>(true);

            ////вершины
            //g.AddVertex("Status1");
            //g.AddVertex("Status2");
            //g.AddVertex("Status3");
            //g.AddVertex("Status4");

            ////ребра
            //g.AddEdge(new TaggedEdge<string, string>("Status1", "Status2", "1"));
            //g.AddEdge(new TaggedEdge<string, string>("Status2", "Status1", "4"));
            //g.AddEdge(new TaggedEdge<string, string>("Status1", "Status3", "2"));
            //g.AddEdge(new TaggedEdge<string, string>("Status3", "Status4", "3"));


            ////foreach (var elem in g.TopologicalSort<string, TaggedEdge<string, string>>())

            ////{

            ////    Console.WriteLine(elem);

            ////}


            //var graphViz = new GraphvizAlgorithm<string, TaggedEdge<string, string>>(g, @".\", QuickGraph.Graphviz.Dot.GraphvizImageType.Png);

            ////стилизация вершин и связей через события
            //graphViz.FormatVertex += FormatVertex;
            //graphViz.FormatEdge += FormatEdge;


            ////graphViz.

            //string res  = graphViz.Generate(new FileDotEngine(), @"E:\first");




            ////поиск в глубину, обнаружение циклов
            ////var dfs = new DepthFirstSearchAlgorithm<Vertex, Edge>(g);
            //////do the search
            ////dfs.Compute();



            ////ViewBag.Result = res;
            //// ViewBag.Output = graphViz.Output.ToString(); ; //dot code
            ////string dotcode = graphViz.Output.ToString();
            #endregion


            #endregion

        }

        /// <summary>
        /// Поиск в глубину цикла в графе
        /// </summary>
        public string DFS(DotGraph<string> graph)
        {
            int countVert = graph.AllVertices.Count();
            List<DotVertex<string>> adv = graph.AllVertices.ToList();
            List<Int32>[] adj = new List<int>[countVert]; //массив смежности для каждой вершины
            bool[] visited = new bool[countVert]; //посещенные узлы
        
            //заполним массив смежности у каждой вершины
            foreach (var ve in graph.VerticesEdges)
            {
              /* Example : vertices=4
               * Source->[Destination, Destination]
               *      0->[1,2]
               *      1->[2]
               *      2->[0,3]
               *      3->[]
               */

                for (int i=0; i< adv.Count; i++ )
                {
                    if (adj[i] == null) adj[i] = new List<int>();
                    if (adv[i].Id == ve.Source.Id)
                    {
                        adj[i].Add(adv.IndexOf(ve.Destination));
                    }
                }
            }


            int startDFSFromVertNumber = 0; //старт обхода с узла №
            Stack<int> stack = new Stack<int>();
            visited[startDFSFromVertNumber] = true; //пометили узел, что посетили
            stack.Push(startDFSFromVertNumber); //добавили узел 

            while (stack.Count != 0)
            {
                startDFSFromVertNumber = stack.Pop();//удалили и вернули удалённый элемент
                
                foreach (int i in adj[startDFSFromVertNumber])
                {
                    if (!visited[i])
                    {
                        visited[i] = true;
                        stack.Push(i); //добавили узел 
                    }
                    else
                    {
                        //нашли цикл
                        string str = $"В ребре {adv[startDFSFromVertNumber].Id}-> {adv[i].Id}";
                        //foreach (int f in adj[startDFSFromVertNumber])
                        //{
                        //    str += adv[f].Id+"->";
                        //}
                        //str = str.Remove(str.Length - 2);
                        return $"Нашли цикл: {str}";
                    }
                }
            }

            return "";
            //сюда зашли значит не нашли цикла
        }

        /// <summary>
        /// Визуализация графа через GraphViz программно.
        /// </summary>
        /// <param name="dotcodeforgraph">dot code</param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult ShowGraph(string dotcodeforgraph)
        {
            try
            {
                if (!String.IsNullOrEmpty(dotcodeforgraph))
                {
                    var parser = AntlrParserAdapter<string>.GetParser();
                    var graph = parser.Parse(dotcodeforgraph);//кавычки в названии вершин не распарсит!
                    if (graph != null && graph.AllVertices.Count()>0)
                    {

                        string layout = "dot";
                        string imagetype = "png";
                        string dotcode = dotcodeforgraph;

                        #region dot 
                        //НЕ ЗАБЫВАЙ КАВЫЧКИ ЭКРАНИРОВАТЬ
                        //@" /*направленный граф*/
                        //    digraph G {
                        ///*вершины*/
                        //StatusOPTIONAL [color = ""skyblue"", style=""filled""];
                        //StatusMANDATORY[color = ""grey"", style = ""filled""];
                        //StatusSTOPSTATUS[color = ""red"", style = ""filled""];

                        ///*связи*/
                        //StatusOPTIONAL-> StatusMANDATORY[label = ""1""];
                        //StatusOPTIONAL-> StatusMANDATORY[label = ""2,5""];
                        //StatusOPTIONAL-> StatusSTOPSTATUS[label = ""1""];

                        //}";

                        #endregion

                        //через процесс с сохранением файлов на диске
                        //byte[] ressimage = ClsForInteractionWithGraphvizThroughProcess.CreateImage(dc, imagetype);
                        //ViewBag.Image3 = ressimage;

                        //через dllки
                        byte[] img = ClsForInteractionWithGraphviz.RenderImage(dotcode, layout, imagetype);
                        if (img == null || img.Length == 0)
                        {
                            ViewBag.Error = "Не удалось получить картинку";
                            return PartialView("ParseNotSuccess");
                        }
                        ViewBag.Image3 = img;


                        return PartialView();
                      
                    }
                    else
                    {
                        ViewBag.Error = "Не удалось распарсить dot код";
                        return PartialView("ParseNotSuccess");
                    }
                    
                }
                else
                {
                    ViewBag.Error = "Пустая строка в dot";
                    return PartialView("ParseNotSuccess");
                }

            }
            catch (Exception e)
            {
                ViewBag.Error = "Поймали исключение:\n" + e.Message + " в методе:" + e.TargetSite + " StackTrace:" + e.StackTrace + "";
                return PartialView("ParseNotSuccess");
            }
        }

        /// <summary>
        /// Загрузка графа в БД.
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public ActionResult InsertIntoPostgreSQL(string nameNewStatusModel)
        {
            try
            {
                
                StatusModel Sm = (StatusModel)TempData["StatusModel"];
                if (Sm != null)
                {
                    var s = Sm.Statuses.Count;
                    Sm.Name = nameNewStatusModel;
                }
                else
                {
                    ViewBag.Error = "Не удалось загрузить StatusModel";
                    return PartialView("ParseNotSuccess");
                }

                String connectionString = "Server=localhost;Port=5432;User Id=postgres;Password=1234;Database=Graphs;";

                using (NpgsqlConnection npgSqlConnection = new NpgsqlConnection(connectionString))
                {
                    npgSqlConnection.Open();

                    //статусную модель добавляем 
                    NpgsqlCommand objectsSqlCommand = new NpgsqlCommand("INSERT INTO objects (object_name) VALUES (@name) RETURNING object_id", npgSqlConnection);
                    objectsSqlCommand.Parameters.AddWithValue("name", Sm.Name);
                    // objectsSqlCommand.ExecuteNonQuery();
                    Sm.IdFromDB = (int)objectsSqlCommand.ExecuteScalar();

                    ViewBag.IdObject = Sm.IdFromDB;


                    //статусы создаем
                    NpgsqlCommand statusesSqlCommand =
                        new NpgsqlCommand("INSERT INTO statuses (object_id, status_desc, status_name, status_type) VALUES ", npgSqlConnection);

                    for (int i =0; Sm.Statuses.Count > i; i++)
                    {
                        
                        statusesSqlCommand.CommandText += $"(@objectid{i}, @desc{i}, @name{i}, @type{i}), ";

                        statusesSqlCommand.Parameters.AddWithValue($"objectid{i}", Sm.IdFromDB);
                        statusesSqlCommand.Parameters.AddWithValue($"desc{i}", Sm.Statuses[i].Describe);
                        statusesSqlCommand.Parameters.AddWithValue($"name{i}", Sm.Statuses[i].Name);
                        statusesSqlCommand.Parameters.AddWithValue($"type{i}", Sm.Statuses[i].Type);
                        
                    }
                    statusesSqlCommand.CommandText = statusesSqlCommand.CommandText.Remove(statusesSqlCommand.CommandText.Length - 2);
                    statusesSqlCommand.CommandText += " RETURNING status_id";

                    //int statusesid = (int)statusesSqlCommand.ExecuteScalar();
                    NpgsqlDataReader Reader = statusesSqlCommand.ExecuteReader();

                    int countLoadStatuses = 0;
                    if (Reader.HasRows)
                    {
                        while (Reader.Read())
                        {
                            Sm.Statuses[countLoadStatuses].IdFromDB= Reader.GetInt32(0);
                            countLoadStatuses++;
                        }
                    }

                    Reader.Close();
                    ViewBag.CountLoadStatuses = countLoadStatuses;


                    //связи создаем
                    NpgsqlCommand workflowsSqlCommand =
                        new NpgsqlCommand("INSERT INTO workflows (status_id_prev, status_id_next) VALUES ", npgSqlConnection);

                    for (int i =0; Sm.Workflows.Count>i;i++)
                    {
                        workflowsSqlCommand.CommandText += $"(@idprev{i}, @idnext{i}), ";

                        workflowsSqlCommand.Parameters.AddWithValue($"idprev{i}", Sm.Workflows[i].PrevStatus.IdFromDB);
                        workflowsSqlCommand.Parameters.AddWithValue($"idnext{i}", Sm.Workflows[i].NextStatus.IdFromDB);
                    }
                    workflowsSqlCommand.CommandText = workflowsSqlCommand.CommandText.Remove(workflowsSqlCommand.CommandText.Length - 2);
                    workflowsSqlCommand.CommandText +=" RETURNING workflow_id";
                    //workflowsSqlCommand.ExecuteNonQuery();

                    Reader = workflowsSqlCommand.ExecuteReader();

                    int countLoadWorkflows = 0;
                    if (Reader.HasRows)
                    {
                        while (Reader.Read())
                        {
                            Sm.Workflows[countLoadWorkflows].IdFromDB = Reader.GetInt32(0);
                            countLoadWorkflows++;
                        }
                    }

                    Reader.Close();
                    ViewBag.CountLoadWorkflows = countLoadWorkflows;


                    npgSqlConnection.Close();

                    return View();
                }
            }
            catch (Exception e)
            {
                ViewBag.Error = "Поймали исключение:\n" + e.Message +" в методе:"+ e.TargetSite + " StackTrace:" + e.StackTrace+"";
                return PartialView("ParseNotSuccess");
            }

        }


        public ActionResult Index()
        {

            //string dotcode =

            //                    @" /*направленный граф*/
            //    digraph G {
            ///*вершины*/
            //StatusOPTIONAL [color = ""blue"", style=""filled""];
            //StatusMANDATORY[color = ""grey"", style = ""filled""];
            //StatusSTOPSTATUS[color = ""red"", style = ""filled""];

            ///*связи*/
            //StatusOPTIONAL-> StatusMANDATORY[label = ""1""];
            //StatusOPTIONAL-> StatusMANDATORY[label = ""2,5""];


            //}";


            #region получение картинки

            // ViewBag.Output = dotcode;
            // string layout = "dot";
            // string imagetype = "png";

            //вариант1 рендеринг картинки через загрузку сшных библиотек и взаимодействие с ними
            //todo сбиты шрифты, криво выводит метки, не может загрузить dllку pango... проблема в пакете graphviz 2.38. Решение найти и попробовать более ранюю версию
            //todo со старой версией не запустилось, но там устанавливал пакет через msi и в консольке проблема ушла, проверяю здесь.
            //byte[] img = ClsForInteractionWithGraphviz.RenderImage(dotcode, layout, imagetype);
            //if (img == null || img.Length == 0)
            //{
            //    throw new Exception("Image is null");
            //}

            ////не надо
            //byte[] b;
            //using (MemoryStream stream = new MemoryStream(img))
            //{
            //    //Image image1 = Image.FromStream(stream);
            //    //image1.Save("pict.png", System.Drawing.Imaging.ImageFormat.Png);
            //    b = stream.ToArray();

            //}
            ////в представление вынести
            ////<p> @Html.Raw("<img style='width:500px; height:500px;' src=\"data:image/jpeg;base64," + Convert.ToBase64String(@ViewBag.Image1) + "\" />") </ p >
            // ViewBag.Image2 = img;

            //вариант2 рендеринг картинки через запуск нового процесса

            /*
             *  <identity impersonate="true" />
                <authentication mode="Windows">   </authentication>
            */
            //byte[] ressimage = ClsForInteractionWithGraphvizThroughProcess.CreateImage(dotcode, imagetype);
            //ViewBag.Image2 = ressimage;

            #endregion



            return View();


        }


        #region metods of quickgraph


        //private static void FormatVertex(object sender, FormatVertexEventArgs<string> e)

        //{

        //    e.VertexFormatter.Label = e.Vertex;

        //    e.VertexFormatter.Shape = QuickGraph.Graphviz.Dot.GraphvizVertexShape.Circle;

        //    //e.VertexFormatter.StrokeColor = Color.Yellow;

        //    //e.VertexFormatter.Font = new Font(FontFamily.GenericSansSerif, 12);

        //}


        //private static void FormatEdge(object sender, FormatEdgeEventArgs<string, TaggedEdge<string, string>> e)

        //{

        //    e.EdgeFormatter.Head.Label = e.Edge.Target;

        //    e.EdgeFormatter.Tail.Label = e.Edge.Source;

        //   // e.EdgeFormatter.Font = new Font(FontFamily.GenericSansSerif, 8);

        //   // e.EdgeFormatter.FontColor = Color.Red;

        //   // e.EdgeFormatter.StrokeColor = Color.Gray;

        //}
        #endregion


    }

    //для получения файла .dot в quickgraph
    //public class FileDotEngine : IDotEngine
    //{
    //    public string Run(GraphvizImageType imageType, string dot, string outputFileName)
    //    {

    //        string output = outputFileName + ".dot";
    //        File.WriteAllText(output, dot);
    //        return output;
    //    }
    //}

}