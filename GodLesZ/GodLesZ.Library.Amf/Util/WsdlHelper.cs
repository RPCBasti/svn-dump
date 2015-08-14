using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Web;
using System.Web.Services.Description;
using System.Web.Services.Discovery;
using System.Xml;
using System.Xml.Serialization;
using GodLesZ.Library.Amf.Configuration;
using GodLesZ.Library.Amf.Exceptions;
using log4net;
using Microsoft.CSharp;

namespace GodLesZ.Library.Amf.Util {
	/// <summary>
	/// This type supports the infrastructure and is not intended to be used directly from your code.
	/// </summary>
	sealed public class WsdlHelper {
		private static readonly ILog log = LogManager.GetLogger(typeof(WsdlHelper));

		static Hashtable _webserviceTypeCache = new Hashtable();
		static object _objLock = new object();
		/// <summary>
		/// Initializes a new instance of the WsdlHelper class.
		/// </summary>
		private WsdlHelper() {
		}

		private static string GetApplicationPath() {
			string applicationPath = "";

			if ((object)HttpContext.Current.Request.Url != null)
				applicationPath = HttpContext.Current.Request.Url.AbsoluteUri.Substring(
					0, HttpContext.Current.Request.Url.AbsoluteUri.ToLower().IndexOf(
					HttpContext.Current.Request.ApplicationPath.ToLower(),
					HttpContext.Current.Request.Url.AbsoluteUri.ToLower().IndexOf(
					HttpContext.Current.Request.Url.Authority.ToLower()) +
					HttpContext.Current.Request.Url.Authority.Length) +
					HttpContext.Current.Request.ApplicationPath.Length);
			return applicationPath;
		}

		#region Wsdl

		private static string WsdlFromUrl(string url) {
			WebRequest webRequest = WebRequest.Create(url);
			WebResponse result = webRequest.GetResponse();
			Stream responseStream = result.GetResponseStream();
			Encoding encode = System.Text.Encoding.GetEncoding("utf-8");
			StreamReader sr = new StreamReader(responseStream, encode);
			string wsdl = sr.ReadToEnd();
			return wsdl;
		}
		/// <summary>
		/// This method supports the infrastructure and is not intended to be used directly from your code.
		/// </summary>
		/// <param name="asmxFile"></param>
		/// <returns></returns>
		public static Assembly GetAssemblyFromAsmx(string asmxFile) {
			lock (_objLock) {
				Type type = null;
				if (!_webserviceTypeCache.Contains(asmxFile)) {
					Assembly assembly = GenerateAssembly(asmxFile);
					Type[] types = assembly.GetTypes();
					if (types.Length > 0) {
						type = types[0];
						_webserviceTypeCache[asmxFile] = type;
						ObjectFactory.Instance.AddTypeToCache(type);
					}
				} else
					type = _webserviceTypeCache[asmxFile] as Type;
				if (type != null)
					return type.Assembly;
			}
			return null;
		}

		private static Assembly GenerateAssembly(string asmxFile) {
			string strWsdl = WsdlFromUrl(GetApplicationPath() + "/" + asmxFile + "?wsdl");
			// Xml text reader
			StringReader wsdlStringReader = new StringReader(strWsdl);
			XmlTextReader tr = new XmlTextReader(wsdlStringReader);
			ServiceDescription sd = ServiceDescription.Read(tr);
			tr.Close();

			// WSDL service description importer 
			CodeCompileUnit codeCompileUnit = new CodeCompileUnit();
			CodeNamespace codeNamespaceFluorine = new CodeNamespace("GodLesZ.Library.Amf");
			codeCompileUnit.Namespaces.Add(codeNamespaceFluorine);
			CodeNamespace codeNamespace = new CodeNamespace(FluorineConfiguration.Instance.WsdlProxyNamespace);
			codeCompileUnit.Namespaces.Add(codeNamespace);

#if (NET_1_1)
            ServiceDescriptionImporter sdi = new ServiceDescriptionImporter();
            sdi.AddServiceDescription(sd, null, null);
            sdi.ProtocolName = "Soap";
            sdi.Import(codeNamespace, codeCompileUnit);
			CSharpCodeProvider provider = new CSharpCodeProvider();
#else
			// Create a WSDL collection.
			DiscoveryClientDocumentCollection wsdlCollection = new DiscoveryClientDocumentCollection();
			wsdlCollection.Add(asmxFile, sd);
			// Create a web refererence using the WSDL collection.
			WebReference reference = new WebReference(wsdlCollection, codeNamespace);
			reference.ProtocolName = "Soap12";
			// Create a web reference collection.
			WebReferenceCollection references = new WebReferenceCollection();
			references.Add(reference);

			WebReferenceOptions options = new WebReferenceOptions();
			options.Style = ServiceDescriptionImportStyle.Client;
			options.CodeGenerationOptions = CodeGenerationOptions.None;
			options.SchemaImporterExtensions.Add(typeof(DataTableSchemaImporterExtension).AssemblyQualifiedName);

			CSharpCodeProvider provider = new CSharpCodeProvider();
			ServiceDescriptionImporter.GenerateWebReferences(references, provider, codeCompileUnit, options);
			// Compile a proxy client
			//provider.GenerateCodeFromCompileUnit(codeCompileUnit, Console.Out, new CodeGeneratorOptions());

#endif

			//http://support.microsoft.com/default.aspx?scid=kb;en-us;326790
			//http://pluralsight.com/wiki/default.aspx/Craig.RebuildingWsdlExe
			if (!FluorineConfiguration.Instance.WsdlGenerateProxyClasses) {

				//Strip any code that isn't the proxy class itself.
				foreach (CodeNamespace cns in codeCompileUnit.Namespaces) {
					// Remove anything that isn't the proxy itself
					ArrayList typesToRemove = new ArrayList();
					foreach (CodeTypeDeclaration codeType in cns.Types) {
						bool webDerived = false;
						foreach (CodeTypeReference baseType in codeType.BaseTypes) {
							if (baseType.BaseType == "System.Web.Services.Protocols.SoapHttpClientProtocol") {
								webDerived = true;
								break;
							}
						}
						if (!webDerived)
							typesToRemove.Add(codeType);
						else {
							CodeAttributeDeclaration codeAttributeDeclaration = new CodeAttributeDeclaration(typeof(GodLesZ.Library.Amf.RemotingServiceAttribute).FullName);
							codeType.CustomAttributes.Add(codeAttributeDeclaration);
							foreach (CodeTypeMember member in codeType.Members) {
								CodeConstructor ctor = member as CodeConstructor;
								if (ctor != null) {
									// We got a constructor
									// Add CookieContainer code
									// this.CookieContainer = new System.Net.CookieContainer(); //Session Cookie
									CodeSnippetStatement statement = new CodeSnippetStatement("this.CookieContainer = new System.Net.CookieContainer(); //Session Cookie");
									ctor.Statements.Add(statement);
								}
							}
						}
					}

					foreach (CodeTypeDeclaration codeType in typesToRemove) {
						codeNamespace.Types.Remove(codeType);
					}
				}
			} else {
				foreach (CodeNamespace cns in codeCompileUnit.Namespaces) {
					foreach (CodeTypeDeclaration codeType in cns.Types) {
						bool webDerived = false;
						foreach (CodeTypeReference baseType in codeType.BaseTypes) {
							if (baseType.BaseType == "System.Web.Services.Protocols.SoapHttpClientProtocol") {
								webDerived = true;
								break;
							}
						}
						if (webDerived) {
							CodeAttributeDeclaration codeAttributeDeclaration = new CodeAttributeDeclaration(typeof(GodLesZ.Library.Amf.RemotingServiceAttribute).FullName);
							codeType.CustomAttributes.Add(codeAttributeDeclaration);
							foreach (CodeTypeMember member in codeType.Members) {
								CodeConstructor ctor = member as CodeConstructor;
								if (ctor != null) {
									// We got a constructor
									// Add CookieContainer code
									// this.CookieContainer = new System.Net.CookieContainer(); //Session Cookie
									CodeSnippetStatement statement = new CodeSnippetStatement("this.CookieContainer = new System.Net.CookieContainer(); //Session Cookie");
									ctor.Statements.Add(statement);
								}
							}
						}
					}
				}
			}
			if (FluorineConfiguration.Instance.ImportNamespaces != null) {
				for (int i = 0; i < FluorineConfiguration.Instance.ImportNamespaces.Count; i++) {
					ImportNamespace importNamespace = FluorineConfiguration.Instance.ImportNamespaces[i];
					codeNamespace.Imports.Add(new CodeNamespaceImport(importNamespace.Namespace));
				}
			}

			// source code generation
			StringBuilder srcStringBuilder = new StringBuilder();
			StringWriter sw = new StringWriter(srcStringBuilder);
#if (NET_1_1)
			ICodeGenerator icg = provider.CreateGenerator();
			icg.GenerateCodeFromCompileUnit(codeCompileUnit, sw, null);
#else
			provider.GenerateCodeFromCompileUnit(codeCompileUnit, sw, null);
#endif
			string srcWSProxy = srcStringBuilder.ToString();
			sw.Close();

			// assembly compilation.
			CompilerParameters cp = new CompilerParameters();
			cp.ReferencedAssemblies.Add("System.dll");
			cp.ReferencedAssemblies.Add("System.Data.dll");
			cp.ReferencedAssemblies.Add("System.Xml.dll");
			cp.ReferencedAssemblies.Add("System.Web.Services.dll");

			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
				if (assembly.GlobalAssemblyCache) {
					//Only System namespace
					if (assembly.GetName().Name.StartsWith("System")) {
						if (!cp.ReferencedAssemblies.Contains(assembly.GetName().Name + ".dll"))
							cp.ReferencedAssemblies.Add(assembly.Location);
					}
				} else {
					if (assembly.GetName().Name.StartsWith("mscorlib"))
						continue;
					//if( assembly.Location.ToLower().StartsWith(System.Web.HttpRuntime.CodegenDir.ToLower()) )
					//	continue;

					try {
						if (assembly.Location != null && assembly.Location != string.Empty)
							cp.ReferencedAssemblies.Add(assembly.Location);
					} catch (NotSupportedException) {
						//NET2
					}
				}
			}

			cp.GenerateExecutable = false;
			//http://support.microsoft.com/kb/815251
			//http://support.microsoft.com/kb/872800
			cp.GenerateInMemory = false;//true; 
			cp.IncludeDebugInformation = false;
#if (NET_1_1)
			ICodeCompiler icc = provider.CreateCompiler();
			CompilerResults cr = icc.CompileAssemblyFromSource(cp, srcWSProxy);
#else
			CompilerResults cr = provider.CompileAssemblyFromSource(cp, srcWSProxy);
#endif
			if (cr.Errors.Count > 0) {
				StringBuilder sbMessage = new StringBuilder();
				sbMessage.Append(string.Format("Build failed: {0} errors", cr.Errors.Count));
				if (log.IsErrorEnabled)
					log.Error(__Res.GetString(__Res.Wsdl_ProxyGenFail));
				foreach (CompilerError e in cr.Errors) {
					log.Error(__Res.GetString(__Res.Compiler_Error, e.Line, e.Column, e.ErrorText));
					sbMessage.Append("\n");
					sbMessage.Append(e.ErrorText);
				}
				StringBuilder sbSourceTrace = new StringBuilder();
				sbSourceTrace.Append("Attempt to compile the generated source code:");
				sbSourceTrace.Append(Environment.NewLine);
				StringWriter swSourceTrace = new StringWriter(sbSourceTrace);
				IndentedTextWriter itw = new IndentedTextWriter(swSourceTrace, "    ");
				provider.GenerateCodeFromCompileUnit(codeCompileUnit, itw, new CodeGeneratorOptions());
				itw.Close();
				log.Error(sbSourceTrace.ToString());
				throw new FluorineException(sbMessage.ToString());
			}

			return cr.CompiledAssembly;
		}

		#endregion Wsdl

	}
}