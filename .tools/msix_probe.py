import io, os, subprocess
base = os.path.join(os.environ["TEMP"], "mtest")
os.makedirs(os.path.join(base, "assets"), exist_ok=True)
for n in ["StoreLogo.png","Square44x44Logo.png","Square150x150Logo.png"]:
    dst = os.path.join(base, "assets", n)
    if not os.path.exists(dst):
        io.open(dst,"wb").write(io.open("packaging/"+n,"rb").read() if False else io.open(os.path.join("packaging","assets",n),"rb").read())
if not os.path.exists(os.path.join(base,"PubTemplateEditor.exe")):
    io.open(os.path.join(base,"PubTemplateEditor.exe"),"wb").write(b"MZ")
TMPL = """<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
         xmlns:mp="http://schemas.microsoft.com/appx/2014/manifest"
         xmlns:uap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/universal"
         xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"%(ign)s>
  <Identity Name="Draft" Publisher="CN=Draft" Version="1.0.0.0" ProcessorArchitecture="x64" />
  <Properties>
    <DisplayName>Draft</DisplayName>
    <PublisherDisplayName>Draft</PublisherDisplayName>
    <Logo>assets/StoreLogo.png</Logo>
  </Properties>
  <Dependencies>
    <TargetDeviceFamily Name="Windows.%(fam)s" MinVersion="10.0.14393.0" MaxVersionTested="%(mv)s" />
  </Dependencies>
  <Resources><Resource Language="en-US" /></Resources>
  <Applications>
    <Application Id="PubTemplateEditor" Executable="PubTemplateEditor.exe" EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements DisplayName="Draft" Description="Draft app" BackgroundColor="transparent"
                          Square150x150Logo="assets/Square150x150Logo.png" Square44x44Logo="assets/Square44x44Logo.png" />
    </Application>
  </Applications>
  <Capabilities><rescap:Capability Name="runFullTrust" /></Capabilities>
</Package>
"""
def run(tag, fam, mv, ign, sdk="10.0.22621.0"):
    xml = TMPL % dict(fam=fam, mv=mv, ign=(' IgnorableNamespaces="uap mp"' if ign else ""))
    io.open(os.path.join(base,"AppxManifest.xml"),"w",encoding="utf-8").write(xml)
    exe = "C:/Program Files (x86)/Windows Kits/10/bin/%s/x64/makeappx.exe" % sdk
    r = subprocess.run([exe,"pack","/d",base,"/p",os.path.join(base,"out.msix"),"/o"],capture_output=True,text=True)
    errs=[l.strip() for l in (r.stdout+r.stderr).splitlines() if "error info" in l.lower()]
    print("%-24s rc=%s %s" % (tag, r.returncode, (errs[0][:150] if errs else "OK")))
run("desktop-noign",   "Desktop",   "10.0.14393.0", False)
run("desktop-ign",     "Desktop",   "10.0.14393.0", True)
run("universal-noign", "Universal", "10.0.14393.0", False)
for s in ["10.0.14393.0","10.0.16299.0","10.0.17134.0","10.0.26100.0"]: run("sdk-"+s,"Desktop","10.0.26100.0",False,sdk=s)
