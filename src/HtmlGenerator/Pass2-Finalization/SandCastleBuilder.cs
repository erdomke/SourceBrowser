using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    class SandCastleBuilder
    {
        private ProjectGenerator _generator;
        private Compilation _comp;
        private HashSet<INamedTypeSymbol> _declared;
        private string _docFolder;

        public static async Task Write(ProjectGenerator generator, IEnumerable<ISymbol> types, string rootPath)
        {
            await new SandCastleBuilder() { _generator = generator }.WriteFolder(types, rootPath);
        }

        private async Task WriteFolder(IEnumerable<ISymbol> types, string rootPath)
        {
            _comp = await _generator.Project.GetCompilationAsync();

            _docFolder = Path.Combine(rootPath, "doc");
            if (!Directory.Exists(_docFolder)) Directory.CreateDirectory(_docFolder);
            var root = new TreeNode();
            _declared = new HashSet<INamedTypeSymbol>(types.OfType<INamedTypeSymbol>());
            foreach (var decl in _declared.Where(d => !d.IsAnonymousType))
            {
                var mainFile = Path.Combine(rootPath, GetUrl(decl).Url.Replace('/', '\\'));
                Insert(root, decl);
                WriteFile(decl, Path.GetFileName(rootPath), mainFile);
            }

            using (var sw = new StreamWriter(Path.Combine(rootPath, "docindex.html")))
            {
                sw.WriteLine(@"<!DOCTYPE html><html><head><title>Index</title>
<link rel=""stylesheet"" href=""../styles.css"">
<script src=""../scripts.js""></script>
</head><body class=""namespaceExplorerBody""><div class=""headerBody"" style=""position:fixed;top:0;left:0;right:0;height:40px;color:white;padding:10px 15px;font-size:26px"">
  Documentation
</div>
<div style=""position:fixed;top:60px;left:0;width:290px;bottom:0;overflow:auto;padding:5px;"">");
                WriteChildren(root, sw, "../");
                sw.WriteLine(@"</div>
<div style=""position:fixed;top:60px;left:300px;right:0;bottom:0;border-left:1px solid #ccc"">
<iframe name=""s"" style=""width:100%;height:100%;border:0;"">
</iframe>
</div><script>initializeNamespaceExplorer();</script></body></html>");
            }
        }


        private void WriteChildren(TreeNode node, StreamWriter sw, string pathPrefix)
        {
            if (node.Children == null)
            {
                return;
            }

            foreach (var child in node.Children)
            {
                WriteChild(child, sw, pathPrefix);
            }
        }

        private void WriteChild(TreeNode node, StreamWriter sw, string pathPrefix)
        {
            var typeSym = node.Symbol as INamedTypeSymbol;
            if (typeSym != null)
            {
                WriteType(typeSym, sw, node.Count > 0 ? "folderTitle" : "typeTitle", pathPrefix);
            }
            else
            {
                WriteNamespace(node.Title, sw);
            }

            if (node.Count > 0)
            {
                sw.WriteLine("<div class=\"folder\">");
                WriteChildren(node, sw, pathPrefix);
                sw.Write("</div>");
            }

            sw.WriteLine();
        }

        private void WriteNamespace(string title, StreamWriter sw)
        {
            sw.Write(string.Format("<div class=\"folderTitle\">{0}</div>", Markup.HtmlEscape(title)));
        }

        private void WriteType(INamedTypeSymbol typeDeclaration, StreamWriter sw, string className, string pathPrefix)
        {
            var typeUrl = GetUrl(typeDeclaration).Url;
            sw.Write(string.Format("<div class=\"{3}\"><a class=\"tDN\" href=\"{0}\" target=\"s\"><img class=\"tDNI\" src=\"{4}content/icons/{2}.png\" />{1}</a></div>",
                typeUrl,
                Markup.HtmlEscape(typeDeclaration.Name),
                SymbolIdService.GetGlyphNumber(typeDeclaration),
                className,
                pathPrefix));
        }

        private class Link
        {
            public string Url { get; set; }
            public bool TargetTop { get; set; }
        }

        private Link GetUrl(INamedTypeSymbol symbol, bool root = true)
        {
            if (_declared.Contains(symbol))
            {
                return new Link {
                    Url = (root ? "doc/" : "") + SymbolIdService.GetDisplayString(symbol).Replace('.', '_').Replace('<', '[').Replace('>', ']') + ".html",
                    TargetTop = false
                };
            }
            else
            {
                var decl = new DeclaredSymbolInfo(symbol, _generator.AssemblyName);
                return new Link
                {
                    Url = decl.GetUrl(),
                    TargetTop = true
                };
            }
        }

        private void WriteFile(INamedTypeSymbol symbol, string projectName, string filePath)
        {
            using (var sw = new StreamWriter(filePath))
            {
                sw.WriteLine(@"<!DOCTYPE html>
<html><head><title>");
                sw.Write(Markup.HtmlEscape(SymbolIdService.GetName(symbol)));
                sw.WriteLine(@"</title><link rel=""stylesheet"" href=""../../styles.css""><link rel=""stylesheet"" href=""../../sandcastle.css""></head>
<body>
<h1>");
                sw.Write(Markup.HtmlEscape(SymbolIdService.GetName(symbol)));
                sw.Write(" ");
                sw.Write(Markup.HtmlEscape(SymbolKindText.GetSymbolKind(symbol)));
                sw.Write(@"</h1>
<p>To browse the source code for this type, see the <a href=""");
                var declared = new DeclaredSymbolInfo(symbol, _generator.AssemblyName);
                sw.Write(declared.GetUrl());
                sw.Write(@""" target=""_top"">Reference Source</a>.</p>
<p style=""margin-top:0;margin-bottom:0;""><strong>Namespace:</strong> ");
                sw.Write(Markup.HtmlEscape(GetNamespace(symbol)));
                sw.Write(@"</p>
<p style=""margin-top:0;margin-bottom:0;""><strong>Assembly:</strong> ");
                sw.Write(Markup.HtmlEscape(_generator.AssemblyName));
                sw.Write(@"</p>
<p style=""margin-top:0;margin-bottom:0;""><strong>File:</strong> ");
                if (symbol.DeclaringSyntaxReferences.Any())
                {
                    var path = symbol.DeclaringSyntaxReferences[0].SyntaxTree.FilePath;
                    var i = path.LastIndexOf("\\" + projectName + "\\", StringComparison.OrdinalIgnoreCase);
                    sw.Write(path.Substring(i + 2 + projectName.Length));
                }
                else
                {
                    sw.Write("{none}");
                }
                sw.Write(@"</p>
<section>
  <h2>Inheritance Hierarchy</h2>");

                var hierarchy = new List<INamedTypeSymbol>();
                var curr = symbol;
                while (curr != null)
                {
                    hierarchy.Add(curr);
                    curr = curr.BaseType;
                }
                hierarchy.Reverse();
                Link link;
                for (var i = 0; i < hierarchy.Count - 1; i ++)
                {
                    sw.Write(@"<p style=""margin-top:0;margin-bottom:0;padding-left:");
                    sw.Write(i * 10);
                    sw.Write(@"px""><a href=""");
                    link = GetUrl(hierarchy[i], false);
                    sw.Write(link.Url);
                    sw.Write(@""" ");
                    if (link.TargetTop) sw.Write(@"target=""_top"" ");
                    sw.Write(@">");
                    sw.Write(Markup.HtmlEscape(SymbolIdService.GetDisplayString(hierarchy[i])));
                    sw.WriteLine("</a></p>");
                }
                sw.Write(@"<p style=""margin-top:0;margin-bottom:0;padding-left:");
                sw.Write((hierarchy.Count - 1) * 10);
                sw.Write(@"px""><strong>");
                sw.Write(Markup.HtmlEscape(SymbolIdService.GetDisplayString(symbol)));
                sw.WriteLine("</strong></p>");
                foreach (var decl in _declared.Where(d => d.BaseType == symbol))
                {
                    sw.Write(@"<p style=""margin-top:0;margin-bottom:0;padding-left:");
                    sw.Write(hierarchy.Count * 10);
                    sw.Write(@"px""><a href=""");
                    link = GetUrl(decl, false);
                    sw.Write(link.Url);
                    sw.Write(@""" ");
                    if (link.TargetTop) sw.Write(@"target=""_top"" ");
                    sw.Write(@">");
                    sw.Write(Markup.HtmlEscape(SymbolIdService.GetDisplayString(decl)));
                    sw.WriteLine("</a></p>");
                }


                sw.Write(@"</section>

<section>
  <h2>Constructors</h2>");
                WriteTable(sw, symbol.Constructors.Where(c => !c.IsImplicitlyDeclared), symbol);
                sw.Write(@"</section>

<section>
  <h2>Properties</h2>");
                WriteTable(sw, GetProperties(symbol), symbol);
                sw.Write(@"</section>

<section>
  <h2>Methods</h2>");
                WriteTable(sw, GetMethods(symbol), symbol);
                sw.Write(@"</section>

<section>
  <h2>Events</h2>");
                WriteTable(sw, symbol.GetMembers().OfType<IEventSymbol>(), symbol);
                sw.Write(@"</section>

<!--<section>
  <h2>Remarks</h2>
</section>

<section>
  <h2>Examples</h2>
</section>-->
</body>
</html>");
                //symbol.DeclaringSyntaxReferences[0].SyntaxTree.FilePath

            }
        }

        private IEnumerable<IMethodSymbol> GetMethods(INamedTypeSymbol symbol)
        {
            var methods = new List<IMethodSymbol>();
            var overridden = new HashSet<IMethodSymbol>();
            var curr = symbol;
            while (curr != null)
            {
                var currMethods = curr.GetMembers().OfType<IMethodSymbol>().Where(m =>
                    m.MethodKind != MethodKind.Constructor
                    && m.MethodKind != MethodKind.EventAdd
                    && m.MethodKind != MethodKind.EventRaise
                    && m.MethodKind != MethodKind.EventRemove
                    && m.MethodKind != MethodKind.PropertyGet
                    && m.MethodKind != MethodKind.PropertySet
                    && m.MethodKind != MethodKind.SharedConstructor
                    && m.MethodKind != MethodKind.StaticConstructor
                    && m.MethodKind != MethodKind.UserDefinedOperator
                    && !m.IsStatic
                    && !overridden.Contains(m)).ToArray();
                methods.AddRange(currMethods);
                foreach (var method in currMethods.Where(m => m.OverriddenMethod != null))
                {
                    overridden.Add(method.OverriddenMethod);
                }
                curr = curr.BaseType;
            }
            methods.Sort((x, y) => x.Name.CompareTo(y.Name));
            return methods;
        }

        private IEnumerable<IPropertySymbol> GetProperties(INamedTypeSymbol symbol)
        {
            var props = new List<IPropertySymbol>();
            var overridden = new HashSet<IPropertySymbol>();
            var curr = symbol;
            while (curr != null)
            {
                var currProps = curr.GetMembers().OfType<IPropertySymbol>().Where(p =>
                    !overridden.Contains(p)).ToArray();
                props.AddRange(currProps);
                foreach (var prop in currProps.Where(p => p.OverriddenProperty != null))
                {
                    overridden.Add(prop.OverriddenProperty);
                }
                curr = curr.BaseType;
            }
            return props;
        }

        private void WriteTable(StreamWriter sw, IEnumerable<ISymbol> symbols, INamedTypeSymbol parent)
        {
            sw.Write(@"<table class=""ex""><thead><tr><td class=""ex""></td><td class=""ex""></td><td class=""ex"">Name</td><td class=""ex"">Description</td></tr></thead><tbody>");
            foreach (var sym in symbols)
            {
                var name = sym.ToDisplayString(SymbolIdService.MediumNameFormat);
                var i = name.IndexOf('.');
                name = name.Substring(i + 1);
                if (name.StartsWith("~"))
                {
                    name = "Finalize()";
                }

                //return Configuration.BasePath + AssemblyName + "/a.html#" + Serialization.ULongToHexString(ID);
                sw.Write(@"<tr><td class=""ex""><img src=""../../content/icons/");
                sw.Write(SymbolIdService.GetGlyphNumber(sym));
                sw.Write(@".png"" /></td><td class=""ex""><a href=""");
                sw.Write(Configuration.BasePath + _generator.AssemblyName + "/a.html#" + Serialization.ULongToHexString(SymbolIdService.GetIdULong(sym)));
                sw.Write(@""">{}</a></td><td class=""ex"">");
                sw.Write(Markup.HtmlEscape(name));
                sw.Write(@"</td><td class=""ex"">");
                var doc = Documentation.FromSymbol(sym, this);
                sw.Write(doc.DescriptionHtml);
                if (sym.ContainingType != parent)
                {
                    sw.Write(@" (Inherited from <a href=""");
                    var decl = new DeclaredSymbolInfo(sym.ContainingType, _generator.AssemblyName);
                    sw.Write(decl.GetUrl());
                    sw.Write(@""">");
                    sw.Write(decl.Name);
                    sw.Write("</a>)");
                }
                sw.Write(@"</td></tr>");
            }
            sw.Write(@"</tbody></table>");
        }

        private class Documentation
        {
            private Documentation() { }
            private Documentation(ISymbol sym, SandCastleBuilder builder)
            {
                var curr = sym;
                while (curr != null && string.IsNullOrEmpty(this.DescriptionHtml))
                {
                    var comments = curr.GetDocumentationCommentXml();
                    if (!string.IsNullOrEmpty(comments))
                    {
                        try
                        {
                            var xml = XElement.Parse("<a>" + comments + "</a>");
                            var sees = xml.Descendants("see");
                            foreach (var see in sees.ToArray())
                            {
                                if (see.Attribute("cref") != null)
                                {
                                    if (see.Attribute("cref").Value.StartsWith("T:"))
                                    {
                                        var type = builder._comp.GetTypeByMetadataName(see.Attribute("cref").Value.Substring(2));
                                        if (type != null)
                                        {
                                            var decl = new DeclaredSymbolInfo(type, builder._generator.AssemblyName);
                                            see.ReplaceWith(new XElement("a", new XAttribute("href", decl.GetUrl()), decl.Name));
                                        }
                                        else
                                        {
                                            see.ReplaceWith(new XElement("a", new XAttribute("href", "#"), see.Attribute("cref").Value));
                                        }
                                    }
                                    else
                                    {
                                        see.ReplaceWith(new XElement("a", new XAttribute("href", "#"), see.Attribute("cref").Value));
                                    }
                                }
                                else
                                {
                                    see.ReplaceWith(new XElement("a", new XAttribute("href", "#"), see.Value));
                                }
                            }
                            this.Summary = xml.Descendants("summary").FirstOrDefault();
                        }
                        catch (XmlException) { } // Do nothing for now
                    }
                    foreach (var attr in curr.GetAttributes())
                    {
                        if (attr.AttributeClass.Name == "DisplayNameAttribute" && attr.AttributeClass.ContainingNamespace.Name == "ComponentModel")
                        {
                            var arg = attr.ConstructorArguments.FirstOrDefault();
                            this.DisplayName = (arg.Value ?? "").ToString();
                        }
                        else if (attr.AttributeClass.Name == "DescriptionAttribute" && attr.AttributeClass.ContainingNamespace.Name == "ComponentModel")
                        {
                            var arg = attr.ConstructorArguments.FirstOrDefault();
                            this.Description = (arg.Value ?? "").ToString();
                        }
                    }

                    var method = curr as IMethodSymbol;
                    var prop = curr as IPropertySymbol;
                    if (method != null)
                    {
                        curr = method.OverriddenMethod;
                    }
                    else if (prop != null)
                    {
                        curr = prop.OverriddenProperty;
                    }
                    else
                    {
                        curr = null;
                    }
                }

            }

            public string DescriptionHtml
            {
                get
                {
                    if (Summary == null)
                        return Markup.HtmlEscape(Description);
                    if (string.IsNullOrEmpty(Description))
                        return GetHtml(Summary);
                    return Markup.HtmlEscape(Description) + " " + GetHtml(Summary);
                }
            }

            public string Description { get; private set; }
            public string DisplayName { get; private set; }
            public XElement Summary { get; private set; }

            private string GetHtml(XElement element)
            {
                var settings = new XmlWriterSettings();
                settings.ConformanceLevel = ConformanceLevel.Fragment;

                using (var writer = new StringWriter())
                using (var xml = XmlWriter.Create(writer, settings))
                {
                    foreach (var node in element.Nodes())
                    {
                        node.WriteTo(xml);
                    }
                    xml.Flush();
                    return writer.ToString();
                }
            }

            private static Dictionary<ulong, Documentation> _cache
                = new Dictionary<ulong, Documentation>();

            public static Documentation FromSymbol(ISymbol sym, SandCastleBuilder builder)
            {
                if (SymbolIdService.GetDisplayString(sym).StartsWith("System."))
                {
                    Documentation result;
                    var id = SymbolIdService.GetIdULong(sym);
                    if (!_cache.TryGetValue(id, out result))
                    {
                        result = new Documentation(sym, builder);
                        _cache[id] = result;
                    }
                    return result;
                }
                return new Documentation(sym, builder);
            }
        }

        private string GetNamespace(INamedTypeSymbol symbol)
        {
            var description = SymbolIdService.GetDisplayString(symbol);
            if (string.IsNullOrEmpty(description))
            {
                return "";
            }

            int lastDot = description.LastIndexOf('.');
            if (lastDot == -1)
            {
                return "";
            }

            return description.Substring(0, lastDot);
        }

        private TreeNode Insert(TreeNode root, INamedTypeSymbol symbol)
        {
            var namespaceParts = new List<INamespaceSymbol>();
            var currNs = symbol.ContainingNamespace;
            while (currNs != null && !string.IsNullOrEmpty(currNs.Name))
            {
                namespaceParts.Add(currNs);
                currNs = currNs.ContainingNamespace;
            }
            namespaceParts.Reverse();
            var ns = GetOrCreateNode(root, namespaceParts);
            return ns.GetOrCreate(symbol);
        }

        private TreeNode GetOrCreateNode(TreeNode parent, IEnumerable<ISymbol> parts)
        {
            var curr = parent;
            foreach (var part in parts)
            {
                curr = curr.GetOrCreate(part);
            }
            return curr;
        }

        [DebuggerDisplay("{Title} ({Count})")]
        public class TreeNode
        {
            private Dictionary<string, TreeNode> _children;
            public int Count { get { return _children == null ? 0 : _children.Count; } }
            public IEnumerable<TreeNode> Children
            {
                get
                {
                    return _children == null
                        ? Enumerable.Empty<TreeNode>()
                        : _children.Values
                            .OrderBy(n => n.Symbol is INamespaceSymbol ? 1 : 2)
                            .ThenBy(n => n.Title);
                }
            }
            public string Title { get; private set; }
            public ISymbol Symbol { get; private set; }

            public TreeNode()
            {
                this.Title = "";
            }

            private TreeNode GetOrCreate(string title)
            {
                if (_children == null)
                {
                    _children = new Dictionary<string, TreeNode>(StringComparer.OrdinalIgnoreCase);
                }
                TreeNode result = null;
                if (!_children.TryGetValue(title, out result))
                {
                    result = new TreeNode() { Title = title };
                    _children.Add(title, result);
                }
                return result;
            }

            public TreeNode GetOrCreate(ISymbol symbol)
            {
                var result = GetOrCreate(SymbolIdService.GetName(symbol));
                result.Symbol = symbol;
                return result;
            }
        }
    }
}
