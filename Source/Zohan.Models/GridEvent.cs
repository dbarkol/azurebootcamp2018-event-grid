using System;
using System.Collections.Generic;
using System.Text;

namespace Zohan.Models
{
    /// <summary>
    /// Template for an Event Grid topic
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class GridEvent<T> where T : class
    {
        /// <summary>
        /// Identifier for the event grid topic
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Subject for the event grid topic
        /// </summary>
        public string Subject { get; set; }

        /// <summary>
        /// Event type
        /// </summary>
        public string EventType { get; set; }

        /// <summary>
        /// Data for the topic
        /// </summary>
        public T Data { get; set; }

        /// <summary>
        /// Date and time of the event
        /// </summary>
        public DateTime EventTime { get; set; }
    }
}
