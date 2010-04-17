﻿
open FsCheck
open System

printfn "%A" Runner.init.Value

//Gen.shrink (Some 0) |> printfn "%A"
//(Gen.shrink "hb") |> Seq.iter (printfn "%A")

Check.QuickAll <| Type.GetType( "FsCheck.Checks.Random, FsCheck", true)

Check.QuickAll <| Type.GetType( "FsCheck.Checks.Common, FsCheck", true)

Check.QuickAll <| Type.GetType( "FsCheck.Checks.Generator, FsCheck", true)

Check.QuickAll <| Type.GetType( "FsCheck.Checks.Arbitrary, FsCheck", true)

Check.QuickAll <| Type.GetType( "FsCheck.Checks.Property, FsCheck", true)

printfn "Done. Press any key to exit."
Console.ReadKey() |> ignore
