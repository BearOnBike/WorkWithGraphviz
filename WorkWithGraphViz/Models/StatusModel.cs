using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WorkWithGraphViz.Models
{
    /// <summary>
    /// Граф
    /// </summary>
    public class StatusModel
    {
        public int IdFromDB { get; set; }

        public string Name { get; set; }

        public List<Status> Statuses { get; set; }
        public List<Workflow> Workflows { get; set; }

        public StatusModel()
        {
            Statuses = new List<Status>();
            Workflows = new List<Workflow>();
        }


    }


    /// <summary>
    /// Вершины
    /// </summary>
    public class Status
    {
        public int IdFromDB { get; set; }

        public string IdFromDotVertex { get; set; }
        public string Name { get; set; }
        public string Describe { get; set; }
        
        public string Type { get; set; }

    }

    /// <summary>
    /// Связи
    /// </summary>
    public class Workflow
    {
        public int IdFromDB { get; set; }
        public Status NextStatus { get; set; }

        public Status PrevStatus { get; set; }
    }

}