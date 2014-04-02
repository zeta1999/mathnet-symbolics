﻿namespace MathNet.Symbolics

open System
open System.Collections.Generic
open System.Numerics
open MathNet.Numerics
open MathNet.Symbolics

open ExpressionPatterns
open Operators


/// General Polynomial Expressions
module Polynomial =

    let symbols (xs: Expression list) = HashSet(List.toSeq xs, HashIdentity.Structural)

    let variables x =
        let rec impl keep = function
            | Number _ -> ()
            | PosIntPower (r, _) -> keep r
            | Power _ as p -> keep p
            | Sum ax -> ax |> List.iter (impl keep)
            | Product ax -> ax |> List.iter (fun a -> match a with | Sum _ as z -> keep z | _ -> impl keep a)
            | _ as z -> keep z
        let hs = symbols []
        impl (hs.Add >> ignore) x
        hs

    let rec isMonomial symbol = function
        | x when x = symbol -> true
        | Number _ -> true
        | PosIntPower (r, _) when r = symbol -> true
        | Product ax -> List.forall (isMonomial symbol) ax
        | x -> Structure.freeOf symbol x

    let rec isMonomialMV (symbols: HashSet<Expression>) = function
        | x when symbols.Contains(x) -> true
        | Number _ -> true
        | PosIntPower (r, _) when symbols.Contains(r) -> true
        | Product ax -> List.forall (isMonomialMV symbols) ax
        | x -> Structure.freeOfSet symbols x

    let isPolynomial symbol = function
        | Sum ax -> List.forall (isMonomial symbol) ax
        | x when isMonomial symbol x -> true
        | _ -> false

    let isPolynomialMV (symbols: HashSet<Expression>) = function
        | Sum ax -> List.forall (isMonomialMV symbols) ax
        | x when isMonomialMV symbols x -> true
        | _ -> false

    let rec degreeMonomial symbol = function
        | x when x = zero -> NegativeInfinity
        | x when x = symbol -> one
        | Number _ -> zero
        | PosIntPower (r, p) when r = symbol -> p
        | Product ax -> sum <| List.map (degreeMonomial symbol) ax
        | x when Structure.freeOf symbol x -> zero
        | _ -> Undefined

    let rec degreeMonomialMV (symbols: HashSet<Expression>) = function
        | x when x = zero -> NegativeInfinity
        | x when symbols.Contains(x) -> one
        | Number _ -> zero
        | PosIntPower (r, p) when symbols.Contains(r) -> p
        | Product ax -> sum <| List.map (degreeMonomialMV symbols) ax
        | x when Structure.freeOfSet symbols x -> zero
        | _ -> Undefined

    let degree symbol x =
        let d = degreeMonomial symbol x
        if d <> Undefined then d else
        match x with
        | Sum ax -> Numbers.max <| List.map (degreeMonomial symbol) ax
        | _ -> Undefined

    let degreeMV (symbols: HashSet<Expression>) x =
        let d = degreeMonomialMV symbols x
        if d <> Undefined then d else
        match x with
        | Sum ax -> Numbers.max <| List.map (degreeMonomialMV symbols) ax
        | _ -> Undefined

    let totalDegree x = degreeMV (variables x) x

    let rec coefficientDegreeMonomial symbol = function
        | x when x = symbol -> one, one
        | Number _ as x -> x, zero
        | PosIntPower (r, p) when r = symbol -> one, p
        | Product ax ->
            let cds = List.map (coefficientDegreeMonomial symbol) ax
            product <| List.map fst cds, sum <| List.map snd cds
        | x when Structure.freeOf symbol x -> x, zero
        | _ -> Undefined, Undefined

    let coefficient symbol (k:int) x =
        let ke = number k
        let c, d = coefficientDegreeMonomial symbol x
        if d = ke then c else
        match x with
        | Sum ax -> List.map (coefficientDegreeMonomial symbol) ax |> List.filter (fun (_, d) -> d = ke) |> List.map fst |> sum
        | _ -> Undefined

    let leadingCoefficientDegree symbol x =
        let c, d = coefficientDegreeMonomial symbol x
        if d <> Undefined then c, d else
        match x with
        | Sum ax ->
            let cds = List.map (coefficientDegreeMonomial symbol) ax
            let degree = Numbers.max <| List.map snd cds
            cds |> List.filter (fun (_, d) -> d = degree) |> List.map fst |> sum, degree
        | _ -> Undefined, Undefined

    let leadingCoefficient symbol x = leadingCoefficientDegree symbol x |> fst

    let coefficients symbol x =
        let rec collect symbol = function
            | x when x = symbol -> [1, one]
            | Number _ as a -> [0, a]
            | PosIntPower (r, Number n) when r = symbol -> [int n, one]
            | Sum ax -> List.collect (collect symbol) ax
            | Product ax -> List.map (collect symbol) ax |> List.reduce (fun a b -> a |> List.fold (fun s (o1, e1) -> b |> List.fold (fun s (o2, e2) -> (o1+o2,e1*e2)::s) s) [])
            | x when Structure.freeOf symbol x -> [0, x]
            | _ -> []
        let c = collect symbol x
        let degree = c |> Seq.map fst |> Seq.max
        c |> List.fold (fun (s:Expression[]) (o,e) -> s.[o] <- s.[o] + e; s) (Array.create (degree+1) zero)

    let rec collectTermsMonomial symbol = function
        | x when x = symbol -> (one, x)
        | Number _ as x-> (x, one)
        | PosIntPower (r, p) as x when r = symbol -> (one, x)
        | Product ax -> List.map (collectTermsMonomial symbol) ax |> List.reduce (fun (c1, v1) (c2, v2) -> (c1*c2, v1*v2))
        | x when Structure.freeOf symbol x -> (x, one)
        | _ -> (Undefined, Undefined)

    let rec collectTermsMonomialMV (symbols: HashSet<Expression>) = function
        | x when symbols.Contains(x) -> (one, x)
        | Number _ as x-> (x, one)
        | PosIntPower (r, p) as x when symbols.Contains(r) -> (one, x)
        | Product ax -> List.map (collectTermsMonomialMV symbols) ax |> List.reduce (fun (c1, v1) (c2, v2) -> (c1*c2, v1*v2))
        | x when Structure.freeOfSet symbols x -> (x, one)
        | _ -> (Undefined, Undefined)

    let collectTerms symbol = function
        | Sum ax -> List.map (collectTermsMonomial symbol) ax |> Seq.groupBy snd |> Seq.map (fun (v, cs) -> (Seq.map fst cs |> sumSeq) * v) |> sumSeq
        | x -> let c, v = collectTermsMonomial symbol x in if c <> Undefined then c*v else Undefined

    let collectTermsMV (symbols: HashSet<Expression>) = function
        | Sum ax -> List.map (collectTermsMonomialMV symbols) ax |> Seq.groupBy snd |> Seq.map (fun (v, cs) -> (Seq.map fst cs |> sumSeq) * v) |> sumSeq
        | x -> let c, v = collectTermsMonomialMV symbols x in if c <> Undefined then c*v else Undefined

    let divide symbol u v =
        let n = degree symbol v
        if Numbers.compare n one < 0 then (u/v |> Algebraic.expand, zero) else
        let lcv = leadingCoefficient symbol v
        let w = v - lcv*symbol**n
        let rec pd q r =
            let m = degree symbol r
            if Numbers.compare m n < 0 then q, r else
            let lcr = leadingCoefficient symbol r
            let s = lcr / lcv
            let z = symbol**(m-n)
            pd (q + s*z) ((r - lcr*symbol**m) - w*s*z |> Algebraic.expand)
        pd zero u

    let quot symbol u v = divide symbol u v |> fst
    let remainder symbol u v = divide symbol u v |> snd

    let polynomialExpansion symbol t u v =
        let rec pe x =
            if x = zero then zero else
            let q, r = divide symbol x v
            t * (pe q) + r |> Algebraic.expand
        pe u |> collectTerms t

    /// Naive polynomial GCD (to be replaced)
    let gcd symbol u v =
        if u = zero && v = zero then zero else
        let rec inner x y =
            if y = zero then x
            else inner y (remainder symbol x y)
        let z = inner u v in z / (leadingCoefficient symbol z) |> Algebraic.expand

    /// Naive polynomial EGCD (to be replaced)
    let extendedGcd symbol u v =
         if u = zero && v = zero then (zero, zero, zero) else
         let rec inner x y a' a'' b' b'' =
            if y = zero then (x, a'', b'') else
            let q, r = divide symbol x y
            inner y r (a'' - q*a') a' (b'' - q*b') b'
         let z, a, b = inner u v zero one one zero
         let c = leadingCoefficient symbol z
         Algebraic.expand (z/c), Algebraic.expand (a/c), Algebraic.expand (b/c)


/// Single-Variable Polynomial (2*x+3*x^2)
module SingleVariablePolynomial =

    let rec isMonomialSV symbol = function
        | x when x = symbol -> true
        | Number _ -> true
        | PosIntPower (r, _) when r = symbol -> true
        | Product ax -> List.forall (isMonomialSV symbol) ax
        | _ -> false

    let isPolynomialSV symbol = function
        | Sum ax -> List.forall (isMonomialSV symbol) ax
        | x when isMonomialSV symbol x -> true
        | _ -> false

    let rec degreeMonomialSV symbol = function
        | x when x = zero -> NegativeInfinity
        | x when x = symbol -> one
        | Number _ -> zero
        | PosIntPower (r, p) when r = symbol -> p
        | Product ax -> sum <| List.map (degreeMonomialSV symbol) ax
        | _ -> Undefined

    let degreeSV symbol x =
        let d = degreeMonomialSV symbol x
        if d <> Undefined then d else
        match x with
        | Sum ax -> Numbers.max <| List.map (degreeMonomialSV symbol) ax
        | _ -> Undefined

    let rec coefficientMonomialSV symbol = function
        | x when x = symbol -> one
        | Number _ as x -> x
        | PosIntPower (r, _) when r = symbol -> one
        | Product ax -> product <| List.map (coefficientMonomialSV symbol) ax
        | _ -> Undefined

    let rec coefficientDegreeMonomialSV symbol = function
        | x when x = zero -> x, NegativeInfinity
        | x when x = symbol -> one, one
        | Number _ as x -> x, zero
        | PosIntPower (r, p) when r = symbol -> one, p
        | Product ax ->
            let cds = List.map (coefficientDegreeMonomialSV symbol) ax
            product <| List.map fst cds, sum <| List.map snd cds
        | _ -> Undefined, Undefined

    let coefficientSV symbol (k:int) x =
        let ke = number k
        let c, d = coefficientDegreeMonomialSV symbol x
        if d = ke then c else
        match x with
        | Sum ax -> List.map (coefficientDegreeMonomialSV symbol) ax |> List.filter (fun (_, d) -> d = ke) |> List.map fst |> sum
        | _ -> Undefined

    let leadingCoefficientDegreeSV symbol x =
        let c, d = coefficientDegreeMonomialSV symbol x
        if d <> Undefined then c, d else
        match x with
        | Sum ax ->
            let cds = List.map (coefficientDegreeMonomialSV symbol) ax
            let degree = Numbers.max <| List.map snd cds
            cds |> List.filter (fun (_, d) -> d = degree) |> List.map fst |> sum, degree
        | _ -> Undefined, Undefined

    let leadingCoefficientSV symbol x = leadingCoefficientDegreeSV symbol x |> fst

    let coefficientsSV symbol x =
        let rec collect symbol = function
            | x when x = symbol -> [1, one]
            | Number _ as a -> [0, a]
            | PosIntPower (r, Number n) when r = symbol -> [int n, one]
            | Sum ax -> List.collect (collect symbol) ax
            | Product ax -> List.map (collect symbol) ax |> List.reduce (fun a b -> a |> List.fold (fun s (o1, e1) -> b |> List.fold (fun s (o2, e2) -> (o1+o2,e1*e2)::s) s) [])
            | _ -> []
        let c = collect symbol x
        let degree = c |> Seq.map fst |> Seq.max
        c |> List.fold (fun (s:Expression[]) (o,e) -> s.[o] <- s.[o] + e; s) (Array.create (degree+1) zero)
