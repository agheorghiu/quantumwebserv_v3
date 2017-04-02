#if BOOTSTRAP
System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__
if not (System.IO.File.Exists "paket.exe") then let url = "https://github.com/fsprojects/Paket/releases/download/3.13.3/paket.exe" in use wc = new System.Net.WebClient() in let tmp = System.IO.Path.GetTempFileName() in wc.DownloadFile(url, tmp); System.IO.File.Move(tmp,System.IO.Path.GetFileName url);;
#r "paket.exe"
Paket.Dependencies.Install (System.IO.File.ReadAllText "paket.dependencies")
#endif

#if INTERACTIVE
#r @"bin/Liquid1.dll"
#else
namespace Microsoft.Research.Liquid
#endif

#load "packages/FsLab/Themes/DefaultWhite.fsx"
#load "packages/FsLab/FsLab.fsx"
#r "packages/Suave/lib/net40/Suave.dll"

open Suave
open Suave.Http
open Suave.Filters
open Suave.Successful

open System
open System.Net

open Deedle
open FSharp.Data
open XPlot.GoogleCharts
open XPlot.Plotly
open XPlot.GoogleCharts.Deedle

open Microsoft.Research.Liquid
open Util
open Operations
open Tests

module Script =
    // public method for Z rotating a particular qubit in the cluster state
    let rotate (q:Qubit) (angle:float) =
        HamiltonianGates.Rpauli angle Z [q]                

    // public method for measuring a qubit in the XY plane
    let rotatedMeasurement (q:Qubit) (angle:float) =
        HamiltonianGates.Rpauli -angle Z [q]
        H [q]
        //HamiltonianGates.Rpauli (Math.PI/2.0) Y [q]
        M [q]


    [<LQD>]
    let test(pad:int, n:int)    =
        let mutable out1 = 0
        let mutable out2 = 0
        let mutable out3 = 0
        let mutable out4 = 0
        let mutable outout = 0
        let k = Ket(4)

        //let pad = 0
        for i in 1..n do
            let qs = k.Reset(4)
            // make Bell state and |+> states
            H >< qs
            H [qs.[1]]
            CNOT [qs.[3]; qs.[1]]
            X [qs.[1]]

            // entangle qubits
            CZ [qs.[0]; qs.[1]]
            CZ [qs.[1]; qs.[2]]

            // server measurements
            rotatedMeasurement qs.[0] (Math.PI / 2.)
            rotatedMeasurement qs.[1] (Math.PI / 4.)
            rotatedMeasurement qs.[2] (Math.PI / 2.)

            //client measurement
            if ((qs.[0].Bit.v + pad) % 2) = 0 then                
                rotatedMeasurement qs.[3] 0.
            else
                rotatedMeasurement qs.[3] (Math.PI / 2.)

            // collect statistics
            out1 <- out1 + qs.[0].Bit.v
            out2 <- out2 + qs.[1].Bit.v
            out3 <- out3 + qs.[2].Bit.v
            out4 <- out4 + qs.[3].Bit.v

            let res = (pad + qs.[0].Bit.v + qs.[1].Bit.v + qs.[2].Bit.v + qs.[3].Bit.v) % 2
            outout <- outout + res
        
        show "Number of ones for each qubit: [%d, %d, %d, %d]" out1 out2 out3 out4
        show "Number of ones in output: %d" outout

        [outout; out1; out2; out3; out4]



let n = 1000
let results0 = Script.test(0, n)
let results1 = Script.test(1, n)

let htmlf = (sprintf """
  <html>
  <head>
      <title>Plotly Chart</title>
      <script src="https://cdn.plot.ly/plotly-latest.min.js"></script>
  </head>
  <body><div id="61f0250d-dbf5-43eb-b33b-b2c348c3acdc" style="width: 700px; height: 500px;"></div>
        <script>
            var data = [{"type":"bar","x":["Client qubit","Server qubit 1","Server qubit 2","Server qubit 3","Server qubit 4"],"y":[%d,%d,%d,%d,%d],"name":"Input 0"},{"type":"bar","x":["Client qubit","Server qubit 1","Server qubit 2","Server qubit 3","Server qubit 4"],"y":[%d,%d,%d,%d,%d],"name":"Input 1"}];
            var layout = {"title":"Output statistics for %d trials","barmode":"group"};
            Plotly.newPlot('61f0250d-dbf5-43eb-b33b-b2c348c3acdc', data, layout);
        </script></body></html>
""")
let html = (htmlf results0.[0] results0.[1] results0.[2] results0.[3] results0.[4] results1.[0] results1.[1] results1.[2] results1.[3] results1.[4] n)


let config = 
    let port = System.Environment.GetEnvironmentVariable("PORT")
    let ip127  = IPAddress.Parse("127.0.0.1")
    let ipZero = IPAddress.Parse("0.0.0.0")

    { defaultConfig with 
        logger = Logging.Loggers.saneDefaultsFor Logging.LogLevel.Verbose
        bindings=[ (if port = null then HttpBinding.mk HTTP ip127 (uint16 8080)
                    else HttpBinding.mk HTTP ipZero (uint16 port)) ] }

let app = OK html
startWebServer config app
