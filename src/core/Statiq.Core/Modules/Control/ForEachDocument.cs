﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Statiq.Common;

namespace Statiq.Core
{
    /// <summary>
    /// Executes the input documents one at a time against the specified child modules.
    /// </summary>
    /// <remarks>
    /// Normally, documents are executed in a breadth-first traversal where all documents
    /// are executed against a module before continuing with the next module. This module
    /// allows you to conduct a depth-first traversal instead by executing each document
    /// one at a time against the child modules before continuing with the next document.
    /// It can be especially helpful when trying to control memory usage for large
    /// documents such as images because it lets you move the documents through the
    /// pipeline one at a time. The aggregate outputs from each sequence of child modules
    /// executed against each document will be output.
    /// </remarks>
    /// <example>
    /// <code>
    /// Pipelines.Add("ImageProcessing",
    ///    // ReadFiles will create N new documents with a Stream
    ///     // (but nothing will be read into memory yet)
    ///     ReadFiles(@"images\*"),
    ///     // Each document will be individually sent through the
    ///     // sequence of ForEach child pipelines
    ///     ForEach(
    ///         // This will load the *current* document into memory
    ///         // and perform image manipulations on it
    ///         ImageProcessor()
    ///             //...
    ///             ,
    ///         // and this will save the stream to disk, replacing it with
    ///         // a file stream, thus freeing up memory for the next file
    ///         WriteFiles()
    ///     )
    /// );
    /// </code>
    /// </example>
    /// <category>Control</category>
    public class ForEachDocument : ContainerModule
    {
        /// <summary>
        /// Specifies the modules to execute against the input document one at a time.
        /// </summary>
        /// <param name="modules">The modules to execute.</param>
        public ForEachDocument(params IModule[] modules)
            : base(modules)
        {
        }

        /// <inheritdoc />
        public override async Task<IEnumerable<IDocument>> ExecuteAsync(IExecutionContext context) =>
            await context.Inputs.SelectManyAsync<IDocument>(context, async input => await context.ExecuteAsync(Children, input.Yield()));
    }
}