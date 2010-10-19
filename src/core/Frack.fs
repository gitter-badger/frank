﻿#nowarn "77"
namespace Frack
open System
open System.Collections.Generic
open System.IO

/// Defines a discriminated union of types that may be provided in the <see cref="Frack.Request"/>.
type Value =
  | Str of string
  | Int of int
  | Err of TextWriter
  | Inp of TextReader
  | Ver of int array

/// Defines the type for a Frack request.
type Request = IDictionary<string, Value>

/// Defines the type for a Frack response.
type Response = int * IDictionary<string, string> * seq<string>

/// Defines the type for a Frack application.
type App = delegate of Request -> Response

/// Defines the type for a Frack middleware.
type Middleware = delegate of App -> Response

[<AutoOpen>]
module Core =
  /// Returns the script name and path info from a url.
  let getPathParts (path:string) =
    if String.IsNullOrEmpty(path) then raise (ArgumentNullException("path")) 
    let p = path.TrimStart('/').Split([|'/'|], 2)  
    let scriptName = if not(String.IsNullOrEmpty(p.[0])) then "/" + p.[0] else String.Empty 
    let pathInfo   = if p.Length > 1 && not(String.IsNullOrEmpty(p.[1])) then "/" + p.[1].TrimEnd('/') else String.Empty 
    (scriptName, pathInfo)


[<AutoOpen>]
module Extensions =
  open System.Collections.Specialized
  open System.Text

  /// Extends System.Collections.Specialized.NameValueCollection with methods to transform it to an enumerable, map or dictionary.
  type NameValueCollection with
    member this.AsEnumerable() = seq { for key in this.Keys do yield (key, Str (this.[key])) }
    member this.ToDictionary() = dict (this.AsEnumerable())
    member this.ToMap() =
      let folder (h:Map<string,string>) (key:string) =
        Map.add key this.[key] h 
      this.AllKeys |> Array.fold (folder) Map.empty


[<AutoOpen>]
module Utility =
  /// Dynamic indexer lookups.
  /// <see href="http://codebetter.com/blogs/matthew.podwysocki/archive/2010/02/05/using-and-abusing-the-f-dynamic-lookup-operator.aspx" />
  let inline (?) this key = ( ^a : (member get_Item : ^b -> ^c) (this,key))
  let inline (?<-) this key value = ( ^a : (member set_Item : ^b * ^c -> ^d) (this,key,value))

  /// Generic duck-typing operator.
  /// <see href="http://weblogs.asp.net/podwysocki/archive/2009/06/11/f-duck-typing-and-structural-typing.aspx" />
  let inline implicit arg = ( ^a : (static member op_Implicit : ^b -> ^a) arg)


module Middlewares =
  let printRequest (app: App) = fun request ->
    let status, hdrs, body = app.Invoke(request)
    let vars = seq { for key in request.Keys do
                       let value = match request.[key] with
                                   | Str(v) -> v
                                   | Int(v) -> v.ToString()
                                   | Err(v) -> v.ToString()
                                   | Inp(v) -> v.ToString()
                                   | Ver(v) -> v.[0].ToString() + "." + v.[1].ToString()
                       yield key + " => " + value }
    let bd = seq { yield! body; yield! vars }
             |> Seq.filter (fun v -> not(String.IsNullOrEmpty(v)))
    ( status, hdrs, bd )