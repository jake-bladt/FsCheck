﻿(*--------------------------------------------------------------------------*\
**  FsCheck                                                                 **
**  Copyright (c) 2008-2010 Kurt Schelfthout. All rights reserved.          **
**  http://www.codeplex.com/fscheck                                         **
**                                                                          **
**  This software is released under the terms of the Revised BSD License.   **
**  See the file License.txt for the full text.                             **
\*--------------------------------------------------------------------------*)

#light

namespace FsCheck

module Arb =

    open TypeClass
    open System

    ///Get the Arbitrary instance for the given type.
    let from<'Value> = Gen.arbitraryInstance<'Value>

    /// Construct an Arbitrary instance from a generator.
    /// Shrink and coarbritrary are not supported for this type.
    let fromGen (gen: Gen<'Value>) : Arbitrary<'Value> =
       { new Arbitrary<'Value>() with
           override x.Generator = gen
       }

    /// Shortcut for constructing an Arbitrary instance from a generator and shrinker.
    /// coarbitrary is not supported for this type.
    let fromGenShrink (gen: Gen<'Value>, shrinker: 'Value -> seq<'Value>): Arbitrary<'Value> =
       { new Arbitrary<'Value>() with
           override x.Generator = gen
           override x.Shrinker a = shrinker a
       }
      
    ///Construct an Arbitrary instance for a type that can be mapped to and from another type (e.g. a wrapper),
    ///based on a Arbitrary instance for the source type and two mapping functions. 
    let convert convertTo convertFrom (a:Arbitrary<'a>) =
        { new Arbitrary<'b>() with
           override x.Generator = a.Generator |> Gen.map convertTo
           override x.CoGenerator b = b |> convertFrom |> a.CoGenerator
           override x.Shrinker b = b |> convertFrom |> a.Shrinker |> Seq.map convertTo
       }

    /// Return an Arbitrary instance that is a filtered version of an existing arbitrary instance.
    /// The generator uses Gen.suchThat, and the shrinks are filtered using Seq.filter with the given predicate.
    let filter pred (a:Arbitrary<'a>) =
        { new Arbitrary<'a>() with
           override x.Generator = a.Generator |> Gen.suchThat pred
           override x.CoGenerator b = b |> a.CoGenerator
           override x.Shrinker b = b |> a.Shrinker |> Seq.filter pred
       }

    /// Return an Arbitrary instance that is a mapped and filtered version of an existing arbitrary instance.
    /// The generator uses Gen.map with the given mapper and then Gen.suchThat with the given predicate, 
    /// and the shrinks are filtered using Seq.filter with the given predicate.
    ///This is sometimes useful if using just a filter would reduce the chance of getting a good value
    ///from the generator - and you can map the value instead. E.g. PositiveInt.
    let mapFilter mapper pred (a:Arbitrary<'a>) =
        { new Arbitrary<'a>() with
           override x.Generator = a.Generator |> Gen.map mapper |> Gen.suchThat pred
           override x.CoGenerator b = b |> a.CoGenerator
           override x.Shrinker b = b |> a.Shrinker |> Seq.filter pred
       }
//TODO
//    /// Generate a subset of an existing set
//    let subsetOf (s: Set<'a>) : Gen<Set<'a>> =
//       gen { // Convert the set into an array
//             let setElems: 'a[] = Array.ofSeq s
//             // Generate indices into the array (up to the number of elements)
//             let! size = Gen.choose(0, s.Count)
//             let! indices = Gen.arrayOfSize (Gen.choose(0, s.Count-1)) size
//             // Extract the elements
//             let arr: 'a[] = indices |> Array.map (fun i -> setElems.[i])
//             // Construct a set (which eliminates dups)
//             return Set.ofArray arr }
//
//    /// Generate a non-empty subset of an existing (non-empty) set
//    let nonEmptySubsetOf (s: Set<'a>) : Gen<Set<'a>> =
//       gen { // Convert the set into an array
//             let setElems: 'a[] = Array.ofSeq s
//             // Generate indices into the array (up to the number of elements)
//             let! size = Gen.choose(1, s.Count)
//             let! indices = Gen.arrayOfLength (Gen.choose(0, s.Count-1)) size
//             // Extract the elements
//             let arr: 'a[] = indices |> Array.map (fun i -> setElems.[i])
//             // Construct a set (which eliminates dups)
//             return Set.ofArray arr }
  
open Gen
open ReflectArbitrary
open System


type NonNegativeInt = NonNegativeInt of int with
    member x.Get = match x with NonNegativeInt r -> r
    static member op_Explicit(NonNegativeInt i) = i

type PositiveInt = PositiveInt of int with
    member x.Get = match x with PositiveInt r -> r
    static member op_Explicit(PositiveInt i) = i

type NonZeroInt = NonZeroInt of int with
    member x.Get = match x with NonZeroInt r -> r
    static member op_Explicit(NonZeroInt i) = i

type NonEmptyString = NonEmptyString of string with
    member x.Get = match x with NonEmptyString r -> r
    static member op_Explicit(NonEmptyString i) = i

type StringNoNulls = StringNoNulls of string with
    member x.Get = match x with StringNoNulls r -> r
    static member op_Explicit(StringNoNulls i) = i

type Interval = Interval of int * int with
    member x.Left = match x with Interval (l,_) -> l
    member x.Right = match x with Interval (_,r) -> r

type IntWithMinMax = IntWithMinMax of int with
    member x.Get = match x with IntWithMinMax r -> r
    static member op_Explicit(IntWithMinMax i) = i

type NonEmptySet<'a when 'a : comparison> = NonEmptySet of Set<'a> with
    member x.Get = match x with NonEmptySet r -> r
    static member toSet(NonEmptySet s) = s
    
type NonEmptyArray<'a> = NonEmptyArray of 'a[] with
    member x.Get = match x with NonEmptyArray r -> r
    static member toArray(NonEmptyArray a) = a

type FixedLengthArray<'a> = FixedLengthArray of 'a[] with
    member x.Get = match x with FixedLengthArray r -> r
    static member toArray(FixedLengthArray a) = a

[<StructuredFormatDisplay("{StructuredDisplayAsTable}")>]
type Function<'a,'b when 'a : comparison> = F of ref<list<('a*'b)>> * ('a ->'b) with
    member x.Value = match x with F (_,f) -> f
    member x.Table = match x with F (table,_) -> !table
    member x.StructuredDisplayAsTable =
        let layoutTuple (x,y) = sprintf "%A->%A" x y
        x.Table 
        |> Seq.distinctBy fst 
        |> Seq.sortBy fst 
        |> Seq.map layoutTuple 
        |> String.concat "; "
        |> sprintf "{ %s }"
    static member from f = 
        let table = ref []
        F (table,fun x -> let y = f x in table := (x,y)::(!table); y)    



///A collection of default generators.
type Default =
    static member private fraction (a:int) (b:int) (c:int) = 
        double a + double b / (abs (double c) + 1.0) 
    ///Generates (), of the unit type.
    static member Unit() = 
        { new Arbitrary<unit>() with
            override x.Generator = gen { return () } 
            override x.CoGenerator g = variant 0
        }
    ///Generates arbitrary bools.
    static member Bool() = 
        { new Arbitrary<bool>() with
            override x.Generator = elements [true; false] 
            override x.CoGenerator b = if b then variant 0 else variant 1
        }
    //byte generator contributed by Steve Gilham.
    ///Generates an arbitrary byte.
    static member Byte() =   
        { new Arbitrary<byte>() with  
            override x.Generator = 
                Gen.choose (0,255) |> Gen.map byte //this is now size independent - 255 is not enough to not cover them all anyway 
            override x.CoGenerator n = n |> int |> variant
            override x.Shrinker n = n |> int |> shrink |> Seq.map byte
        }  
    ///Generate arbitrary int that is between -size and size.
    static member Int() = 
        { new Arbitrary<int>() with
            override x.Generator = sized <| fun n -> choose (-n,n) 
            override x.CoGenerator n = variant (if n >= 0 then 2*n else 2*(-n) + 1)
            override x.Shrinker n = 
                let (|>|) x y = abs x > abs y 
                seq {   if n < 0 then yield -n
                        if n <> 0 then yield 0 
                        yield! Seq.unfold (fun st -> let st = st / 2 in Some (n-st, st)) n 
                                |> Seq.takeWhile ((|>|) n) }
                |> Seq.distinct
        }
    ///Generates arbitrary floats, NaN, NegativeInfinity, PositiveInfinity, Maxvalue, MinValue, Epsilon included fairly frequently.
    static member Float() = 
        { new Arbitrary<float>() with
            override x.Generator = 
                frequency   [(6, map3 Default.fraction arbitrary arbitrary arbitrary)
                            ;(1, elements [ Double.NaN; Double.NegativeInfinity; Double.PositiveInfinity])
                            ;(1, elements [ Double.MaxValue; Double.MinValue; Double.Epsilon])]
            override x.CoGenerator fl = 
                let d1 = sprintf "%g" fl
                let spl = d1.Split([|'.'|])
                let m = if (spl.Length > 1) then spl.[1].Length else 0
                let decodeFloat = (fl * float m |> int, m )
                coarbitrary <| decodeFloat
            override x.Shrinker fl =
                let (|<|) x y = abs x < abs y
                seq {   if Double.IsInfinity fl || Double.IsNaN fl then 
                            yield 0.0
                        else
                            if fl < 0.0 then yield -fl
                            let truncated = truncate fl
                            if truncated |<| fl then yield truncated }
                |> Seq.distinct
        }
    ///Generates arbitrary chars, between ASCII codes Char.MinValue and 127.
    static member Char() = 
        { new Arbitrary<char>() with
            override x.Generator = choose (int Char.MinValue, 127) |> Gen.map char
            override x.CoGenerator c = coarbitrary (int c)
            override x.Shrinker c =
                seq { for c' in ['a';'b';'c'] do if c' < c || not (Char.IsLower c) then yield c' }
        }
    ///Generates arbitrary strings, which are lists of chars generated by Char.
    static member String() = 
        { new Arbitrary<string>() with
            override x.Generator = Gen.map (fun chars -> new String(List.toArray chars)) arbitrary
            override x.CoGenerator s = s.ToCharArray() |> Array.toList |> coarbitrary
            override x.Shrinker s = s.ToCharArray() |> Array.toList |> shrink |> Seq.map (fun chars -> new String(List.toArray chars))
        }
    ///Genereate a 2-tuple.
    static member Tuple2() = 
        { new Arbitrary<'a*'b>() with
            override x.Generator = map2 (fun x y -> (x,y)) arbitrary arbitrary
            //extra paranthesis are needed here, otherwise F# gets confused about the number of arguments
            //and doesn't see that this really overriddes the right method
            override x.CoGenerator ((a,b)) = coarbitrary a >> coarbitrary b
            override x.Shrinker ((x,y)) = 
                seq {   for x' in shrink x -> (x',y ) 
                        for y' in shrink y -> (x ,y') }
        }
    ///Genereate a 3-tuple.
    static member Tuple3() = 
        { new Arbitrary<'a*'b*'c>() with
            override x.Generator = map3 (fun x y z -> (x,y,z)) arbitrary arbitrary arbitrary
            override x.CoGenerator ((a,b,c)) = coarbitrary a >> coarbitrary b >> coarbitrary c
            override x.Shrinker ((x,y,z)) = 
                seq {   for x' in shrink x -> (x',y ,z ) 
                        for y' in shrink y -> (x ,y',z ) 
                        for z' in shrink z -> (x ,y ,z') }
        }
    ///Genereate a 4-tuple.
    static member Tuple4() = 
        { new Arbitrary<'a*'b*'c*'d>() with
            override x.Generator = map4 (fun x y z u-> (x,y,z,u)) arbitrary arbitrary arbitrary arbitrary
            override x.CoGenerator ((a,b,c,d)) = coarbitrary a >> coarbitrary b >> coarbitrary c >> coarbitrary d
            override x.Shrinker ((x,y,z,u)) = 
                seq {   for x' in shrink x -> (x',y ,z ,u ) 
                        for y' in shrink y -> (x ,y',z ,u ) 
                        for z' in shrink z -> (x ,y ,z',u ) 
                        for u' in shrink u -> (x ,y ,z ,u')}
        }
    ///Genereate a 5-tuple.
    static member Tuple5() = 
        { new Arbitrary<'a*'b*'c*'d*'e>() with
            override x.Generator = map5 (fun x y z u v-> (x,y,z,u,v)) arbitrary arbitrary arbitrary arbitrary arbitrary
            override x.CoGenerator ((a,b,c,d,e)) = coarbitrary a >> coarbitrary b >> coarbitrary c >> coarbitrary d >> coarbitrary e
            override x.Shrinker ((x,y,z,u,v)) = 
                seq {   for x' in shrink x -> (x',y ,z ,u ,v ) 
                        for y' in shrink y -> (x ,y',z ,u ,v ) 
                        for z' in shrink z -> (x ,y ,z',u ,v ) 
                        for u' in shrink u -> (x ,y ,z ,u',v )
                        for v' in shrink v -> (x ,y ,z ,u ,v') }
        }
    ///Genereate a 6-tuple.
    static member Tuple6() = 
        { new Arbitrary<'a*'b*'c*'d*'e*'f>() with
            override x.Generator = 
                map6 (fun x y z u v w-> (x,y,z,u,v,w)) arbitrary arbitrary arbitrary arbitrary arbitrary arbitrary
            override x.CoGenerator ((a,b,c,d,e,f)) = 
                coarbitrary a >> coarbitrary b >> coarbitrary c >> coarbitrary d >> coarbitrary e >> coarbitrary f
            override x.Shrinker ((x,y,z,u,v,w)) = 
                seq {   for x' in shrink x -> (x',y ,z ,u ,v ,w ) 
                        for y' in shrink y -> (x ,y',z ,u ,v ,w ) 
                        for z' in shrink z -> (x ,y ,z',u ,v ,w ) 
                        for u' in shrink u -> (x ,y ,z ,u',v ,w )
                        for v' in shrink v -> (x ,y ,z ,u ,v',w )
                        for w' in shrink w -> (x ,y ,z ,u ,v ,w') }
        }
    ///Generate an option value that is 'None' 1/8 of the time.
    static member Option() = 
        { new Arbitrary<option<'a>>() with
            override x.Generator = frequency [(1, gen { return None }); (7, Gen.map Some arbitrary)]
            override x.CoGenerator o = 
                match o with 
                | None -> variant 0
                | Some y -> variant 1 >> coarbitrary y
            override x.Shrinker o =
                match o with
                | Some x -> seq { yield None; for x' in shrink x -> Some x' }
                | None  -> Seq.empty
        }
    ///Generate a list of values. The size of the list is between 0 and the test size + 1.
    static member FsList() = 
        { new Arbitrary<list<'a>>() with
            override x.Generator = listOf arbitrary
            override x.CoGenerator l = 
                match l with
                | [] -> variant 0
                | x::xs -> coarbitrary x << variant 1 << coarbitrary xs
            override x.Shrinker l =
                match l with
                | [] ->         Seq.empty
                | (x::xs) ->    seq { yield xs
                                      for xs' in shrink xs -> x::xs'
                                      for x' in shrink x -> x'::xs }
        }
    ///Generate an object - a boxed char, string or boolean value.
    static member Object() =
        { new Arbitrary<obj>() with
            override x.Generator = 
                oneof [ Gen.map box <| arbitrary<char> ; Gen.map box <| arbitrary<string>; Gen.map box <| arbitrary<bool> ]
            override x.CoGenerator o = 
                match o with
                | :? char as c -> variant 0 >> coarbitrary c
                | :? string as s -> variant 1 >> coarbitrary s
                | :? bool as b -> variant 2 >> coarbitrary b
                | _ -> failwith "Unknown domain type in coarbitrary of obj"
            override x.Shrinker o =
                seq {
                    match o with
                    | :? char as c -> yield box true; yield box false; yield! shrink c |> Seq.map box
                    | :? string as s -> yield box true; yield box false; yield! shrink s |> Seq.map box
                    | :? bool as b -> yield! Seq.empty
                    | _ -> failwith "Unknown type in shrink of obj"
                }
        }
    //Generate a rank 1 array.
    static member Array() =
        { new Arbitrary<'a[]>() with
            override x.Generator = arrayOf arbitrary
            override x.CoGenerator a = a |> Array.toList |> coarbitrary
            override x.Shrinker a = a |> Array.toList |> shrink |> Seq.map List.toArray
        }


    static member Array2D() = 
        Arb.fromGen <| array2DOf arbitrary

     ///Generate a function value. Function values can be generated for types 'a->'b where 'a has a CoArbitrary
     ///value and 'b has an Arbitrary value.
    static member Arrow() = 
        { new Arbitrary<'a->'b>() with
            override x.Generator = promote (fun a -> coarbitrary a arbitrary)
            override x.CoGenerator f = 
                (fun gn -> gen {let x = arbitrary
                                return! coarbitrary (Gen.map f x) gn }) 
        }

    ///Generate a Function value that can be printed and shrunk. Function values can be generated for types 'a->'b where 'a has a CoArbitrary
     ///value and 'b has an Arbitrary value.
    static member Function() =
        { new Arbitrary<Function<'a,'b>>() with
            override x.Generator = Gen.map Function<'a,'b>.from arbitrary
            override x.Shrinker f = 
                let update x' y' f x = if x = x' then y' else f x
                seq { for (x,y) in f.Table do 
                        for y' in shrink y do 
                            yield Function<'a,'b>.from (update x y' f.Value) }
        }

    ///Generates an arbitrary DateTime with no time part. DateTimes are not shrunk.
    static member DateTime() = 
        let date = gen { let! PositiveInt yOffset = Gen.arbitrary
                         let y = 1900 + yOffset
                         let! m = Gen.choose(1, 12)
                         let! d = Gen.choose(1, DateTime.DaysInMonth(y, m))
                         return System.DateTime(y, m, d) }
        Arb.fromGen date      

    static member NonNegativeInt() =
       Arb.from<int> 
       |> Arb.mapFilter abs (fun i -> i >= 0)
       |> Arb.convert NonNegativeInt int

    static member PositiveInt() =
        Arb.from<int>
        |> Arb.mapFilter abs (fun i -> i > 0)
        |> Arb.convert PositiveInt int

    static member NonZeroInt() =
       Arb.from<int>
        |> Arb.filter ((<>) 0)
        |> Arb.convert NonZeroInt int

    static member IntWithMinMax() =
        { new Arbitrary<IntWithMinMax>() with
            override x.Generator = frequency    [ (1 ,elements [Int32.MaxValue; Int32.MinValue])
                                                  (10,arbitrary) ] 
                                   |> Gen.map IntWithMinMax
            override x.CoGenerator (IntWithMinMax i) = coarbitrary i
            override x.Shrinker (IntWithMinMax i) = shrink i |> Seq.map IntWithMinMax }

    ///Generates an interval between two nonnegative integers.
    static member Interval() =
        { new Arbitrary<Interval>() with
            override  x.Generator = 
                gen { let! start,offset = two arbitrary
                      return Interval (abs start,abs start+abs offset) } //TODO: shrinker
        }

    static member StringWithoutNullChars() =
        Arb.from<string>
        |> Arb.filter (not << String.exists ((=) '\000'))
        |> Arb.convert StringNoNulls string

    static member NonEmptyString() =
        Arb.from<string>
        |> Arb.filter (fun s -> s <> "" && not (String.exists ((=) '\000') s))
        |> Arb.convert NonEmptyString string

    static member Set() = 
        Arb.from<list<_>> 
        |> Arb.convert Set.ofList Set.toList

    static member Map() = 
        Arb.from<list<_>> 
        |> Arb.convert Map.ofList Map.toList

    static member NonEmptyArray() =
        Arb.from<_[]>
        |> Arb.filter (fun a -> Array.length a > 0)
        |> Arb.convert NonEmptyArray (fun (NonEmptyArray s) -> s)

    static member NonEmptySet() =
        Arb.from<Set<_>>
        |> Arb.filter (not << Set.isEmpty) 
        |> Arb.convert NonEmptySet (fun (NonEmptySet s) -> s)

    //Arrays whose length does not change when shrinking.
    static member FixedLengthArray() =
        { new Arbitrary<'a[]>() with
            override x.Generator = arbitrary
            override x.CoGenerator a = coarbitrary a
            override x.Shrinker a = a |> Seq.mapi (fun i x -> Gen.shrink x |> Seq.map (fun x' ->
                                                       let data' = Array.copy a
                                                       data'.[i] <- x'
                                                       data')
                                               ) |> Seq.concat
        }
        |> Arb.convert FixedLengthArray (fun (FixedLengthArray a) -> a)

    ///Try to derive an arbitrary instance for the given type reflectively. Works
    ///for record, union, tuple and enum types.
    static member Derive() =
        { new Arbitrary<'a>() with
            override x.Generator = reflectGen
            override x.Shrinker a = reflectShrink a
        }
        
    //TODO: consider assing sbyte, float32, int16, int64, BigInteger, decimal, Generic.Collections types, TimeSpan