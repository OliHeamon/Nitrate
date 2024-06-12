﻿using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Terraria.ModLoader.Setup.Common.Tasks.Nitrate.Patches.Analyzers;

namespace Terraria.ModLoader.Setup.Common.Tasks.Nitrate.Patches;

/// <summary>
///		Applies advanced Terraria/Terraria.ModLoader-specific C# code analyzers.
/// </summary>
/// <remarks>
///		Allows for further control over formatting and applying source-code
///		optimizations.
/// </remarks>
// ReSharper disable once ClassNeverInstantiated.Global
internal sealed class ApplyTerrariaAnalyzers(CommonContext ctx, string sourceDirectory, string targetDirectory) : SetupOperation(ctx)
{
	public override void Run()
	{
		var items = new List<WorkItem>();

		foreach (var (file, relPath) in EnumerateFiles(sourceDirectory))
		{
			var destination = Path.Combine(targetDirectory, relPath);

			// If not a C# source file, just copy and move on.
			if (!relPath.EndsWith(".cs"))
			{
				copy(file, relPath, destination);
				continue;
			}

			items.Add(
				new WorkItem(
					"Rewriting (syntax): " + relPath,
					() =>
					{
						CreateParentDirectory(destination);

						if (File.Exists(destination))
						{
							File.SetAttributes(destination, FileAttributes.Normal);
						}

						File.WriteAllText(destination, Rewrite(File.ReadAllText(file), Context.TaskInterface.CancellationToken));
					}
				)
			);
		}

		ExecuteParallel(items);

		using var workspace = Context.CreateWorkspace();

		// Temporary for debugging.
		workspace.WorkspaceFailed += (__, e) =>
		{
			_ = e.Diagnostic.Message;
		};

		// TODO: In this context, most references should be auto-resolved.
		var status = Context.Progress.CreateStatus(0, 2);
		status.AddMessage("Opening ReLogic.csproj...");
		var relogicProject = workspace.OpenProjectAsync(Path.Combine(targetDirectory, "ReLogic", "ReLogic.csproj")).GetAwaiter().GetResult();
		status.AddMessage("Opened ReLogic.csproj!");
		status.Current++;
		status.AddMessage("Opening Terraria.csproj...");
		var terrariaProject = workspace.OpenProjectAsync(Path.Combine(targetDirectory, "Terraria", "Terraria.csproj")).GetAwaiter().GetResult();
		status.AddMessage("Opened Terraria.csproj!");
		status.Current++;

		var analyzers = new AbstractAnalyzer[]
		{
			new SimplifyRandomAnalyzer("Terraria.Utilities.UnifiedRandom"),
		};

		AnalyzeAndFixProjectsAsync(analyzers, relogicProject, terrariaProject).GetAwaiter().GetResult();

		return;

		void copy(string file, string relPath, string destination)
		{
			items.Add(new WorkItem("Copying: " + relPath, () => Copy(file, destination)));
		}
	}

	private static string Rewrite(string source, CancellationToken cancellationToken)
	{
		var tree = CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken);
		var node = tree.GetRoot(cancellationToken);
		// node = new SimplifyLocalPlayerAccess().Visit(node);
		// node = new SimplifyUnifiedRandom().Visit(node);

		return node.ToFullString();
	}

	private async Task AnalyzeAndFixProjectsAsync(AbstractAnalyzer[] analyzers, params Project[] projects)
	{
		foreach (var project in projects)
		{
			var modifiedDocuments = await AnalyzeAndFixProjectAsync(project, analyzers, Context.TaskInterface.CancellationToken);
			foreach (var document in modifiedDocuments)
			{
				await File.WriteAllTextAsync(document.FilePath!, (await document.GetTextAsync(Context.TaskInterface.CancellationToken)).ToString(), Context.TaskInterface.CancellationToken);
			}
		}
	}

	private async Task<List<Document>> AnalyzeAndFixProjectAsync(Project project, AbstractAnalyzer[] analyzers, CancellationToken cancellationToken)
	{
		var modifiedDocuments = new ConcurrentDictionary<string, Document>();

		var status = Context.Progress.CreateStatus(0, 1);
		status.AddMessage($"Getting compilation for project \"{project.Name}\"...");
		var compilation = await project.GetCompilationAsync(cancellationToken);
		status.AddMessage("Got compilation!");
		status.Current++;
		if (compilation is null)
		{
			throw new DataException("Failed to get compilation for project.");
		}

		var items = new List<WorkItem>();

		foreach (var document in project.Documents)
		{
			items.Add(
				new WorkItem(
					$"Processing document: {document.FilePath![Path.GetDirectoryName(project.FilePath!)!.Length..]}...",
					() =>
					{
						var newDocument = document;

						foreach (var analyzer in analyzers)
						{
							var modifiedDocument = analyzer.ProcessDocument(newDocument);

							if (modifiedDocument is not null)
							{
								newDocument = modifiedDocument;
							}
						}

						if (newDocument != document)
						{
							modifiedDocuments[document.FilePath!] = newDocument;
						}
					}
				)
			);
		}

		ExecuteParallel(items);

		/*
		foreach (var documentId in project.DocumentIds)
		{
			var document = project.GetDocument(documentId);
			var formattedDocument = await Formatter.FormatAsync(document);
			project = formattedDocument.Project;
		}
		*/

		return modifiedDocuments.Values.ToList();
	}

	private static SyntaxNode ApplyTrivia(SyntaxNode node, SyntaxNode original)
	{
		if (original.HasLeadingTrivia)
		{
			node = node.WithLeadingTrivia(original.GetLeadingTrivia());
		}

		if (original.HasTrailingTrivia)
		{
			node = node.WithTrailingTrivia(original.GetTrailingTrivia());
		}

		return node;
	}
}
