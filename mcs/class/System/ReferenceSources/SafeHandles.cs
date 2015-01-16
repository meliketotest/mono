//
// SafeHandles.cs
//
// Author:
//       Martin Baulig <martin.baulig@xamarin.com>
//
// Copyright (c) 2015 Xamarin Inc. (http://www.xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

#if MONO_INSIDE_SYSTEM || MONO_SECURITY_ALIAS
extern alias MonoSecurity;
#if MONO_SECURITY_ALIAS
using X509Certificate2 = MonoSecurity::System.Security.Cryptography.X509Certificates.X509Certificate2;
#else
using X509Certificate2 = System.Security.Cryptography.X509Certificates.X509Certificate2;
#endif
#if MONO_FEATURE_NEW_TLS
using TlsContext = MonoSecurity::Mono.Security.Protocol.NewTls.TlsContext;
using TlsException = MonoSecurity::Mono.Security.Protocol.NewTls.TlsException;
#endif
#else
#if MONO_FEATURE_NEW_TLS
using TlsContext = Mono.Security.Protocol.NewTls.TlsContext;
using TlsException = Mono.Security.Protocol.NewTls.TlsException;
#endif
#endif

using System.Runtime.InteropServices;

namespace System.Net.Security
{
	class DummySafeHandle : SafeHandle
	{
		protected DummySafeHandle () : base ((IntPtr)(-1), true)
		{
		}

		protected override bool ReleaseHandle ()
		{
			return true;
		}

		public override bool IsInvalid {
			get { return handle == (IntPtr)(-1); }
		}
	}

	class SafeFreeCertContext : DummySafeHandle
	{
	}

	class SafeFreeCredentials : DummySafeHandle
	{
		SecureCredential credential;

		public X509Certificate2 Certificate {
			get {
				if (IsInvalid)
					throw new ObjectDisposedException ("Certificate");
				return credential.certificate;
			}
		}

		public SafeFreeCredentials (SecureCredential credential)
		{
			this.credential = credential;
			bool success = true;
			DangerousAddRef (ref success);
		}

		public override bool IsInvalid {
			get {
				return credential.certificate == null;
			}
		}

		protected override bool ReleaseHandle ()
		{
			credential.Clear ();
			return base.ReleaseHandle ();
		}
	}

	class SafeDeleteContext : DummySafeHandle
	{
		#if MONO_FEATURE_NEW_TLS
		TlsContext context;

		public TlsContext Context {
			get {
				if (IsInvalid)
					throw new ObjectDisposedException ("Context");
				return context;
			}
		}

		public TlsException LastError {
			get {
				if (context != null)
					return context.LastError;
				return null;
			}
		}

		public SafeDeleteContext (TlsContext context)
		{
			this.context = context;
		}
		#endif

		public override bool IsInvalid {
			get {
				#if MONO_FEATURE_NEW_TLS
				return context == null || !context.IsValid;
				#else
				return true;
				#endif
			}
		}

		protected override bool ReleaseHandle ()
		{
			#if MONO_FEATURE_NEW_TLS
			context.Clear ();
			context = null;
			#endif
			return base.ReleaseHandle ();
		}
	}

	struct SecureCredential
	{
		public const int CurrentVersion = 0x4;

		[Flags]
		public enum Flags
		{
			Zero = 0,
			NoSystemMapper = 0x02,
			NoNameCheck = 0x04,
			ValidateManual = 0x08,
			NoDefaultCred = 0x10,
			ValidateAuto = 0x20,
			UseStrongCrypto = 0x00400000
		}

		int version;
		internal X509Certificate2 certificate;
		SchProtocols protocols;
		EncryptionPolicy policy;

		public SecureCredential (int version, X509Certificate2 certificate, SecureCredential.Flags flags, SchProtocols protocols, EncryptionPolicy policy)
		{
			this.version = version;
			this.certificate = certificate;
			this.protocols = protocols;
			this.policy = policy;
		}

		public void Clear ()
		{
			certificate = null;
		}
	}

	internal class SafeCredentialReference : DummySafeHandle
	{
		//
		// Static cache will return the target handle if found the reference in the table.
		//
		internal SafeFreeCredentials _Target;

		//
		//
		internal static SafeCredentialReference CreateReference (SafeFreeCredentials target)
		{
			SafeCredentialReference result = new SafeCredentialReference (target);
			if (result.IsInvalid)
				return null;

			return result;
		}

		private SafeCredentialReference (SafeFreeCredentials target) : base ()
		{
			// Bumps up the refcount on Target to signify that target handle is statically cached so
			// its dispose should be postponed
			bool b = false;
			try {
				target.DangerousAddRef (ref b);
			} catch {
				if (b) {
					target.DangerousRelease ();
					b = false;
				}
			} finally {
				if (b) {
					_Target = target;
					SetHandle (new IntPtr (0));   // make this handle valid
				}
			}
		}

		override protected bool ReleaseHandle ()
		{
			SafeFreeCredentials target = _Target;
			if (target != null)
				target.DangerousRelease ();
			_Target = null;
			return true;
		}
	}

}
