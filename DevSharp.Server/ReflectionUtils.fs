﻿module DevSharp.Server.ReflectionUtils

open System
open System.Reflection

let typeOfObj = typedefof<obj>
let typeOfVoid = typedefof<Void>
let typeOfUnit = typedefof<unit>
let typeOfString = typedefof<string>
let typeOfFSharpList = typedefof<FSharp.Collections.List<_>>
let typeOfFSharpOption = typedefof<FSharp.Core.Option<_>>
let typeOfSeq = typedefof<System.Collections.Generic.IEnumerable<_>>
let typeOfEnumerable = typedefof<System.Linq.Enumerable>
let typeOfSeqModule = typeOfFSharpList.Assembly.GetType("Microsoft.FSharp.Collections.SeqModule")

let methodEnumerableCast = typeOfEnumerable.GetMethod("Cast")
let methodSeqModuleToList = typeOfSeqModule.GetMethod("ToList")
let methodSeqModuleSingleton = typeOfSeqModule.GetMethod("Singleton")


let isVoidType (t: Type) =
    t = typeOfVoid || t = typeOfUnit

let areSameType (a: Type) (b: Type) =
    if a = b then true
    else (isVoidType a) = (isVoidType b)


let matchGenericInterface (genericInterface: Type) (t: Type) =
    if not genericInterface.IsInterface then failwith "genericInterface must be an interface"
    let typeDef = genericInterface.GetGenericTypeDefinition()
    let concrete = 
        t.GetInterfaces()
        |> Array.tryFind (fun it -> it.IsGenericType && it.GetGenericTypeDefinition() = typeDef)
    match concrete with
    | None -> None
    | Some c -> c.GetGenericArguments() |> Array.toList |> Some

let matchGenericClass (genericClass: Type) (t: Type) =
    if not genericClass.IsClass then failwith "genericClass must be a class"
    let typeDef = genericClass.GetGenericTypeDefinition()
    let concrete = 
        t 
        |> Array.unfold (fun x -> if x.BaseType = typeOfObj then None else Some (x.BaseType, x.BaseType))
        |> Array.tryFind (fun it -> it.IsGenericType && it.GetGenericTypeDefinition() = typeDef)
    match concrete with
    | None -> None
    | Some c -> c.GetGenericArguments() |> Array.toList |> Some


let findModuleInnerType (moduleType: Type) containerName attrType defaultName  (isOptional: bool) =
    let isTheType (t: Type) =
        let attr = t.GetCustomAttributes(attrType, false)
        attr.Length > 0 || t.Name = defaultName

    let types = moduleType.GetNestedTypes(BindingFlags.Public) 
    let candidates = types
                    |> Array.toList
                    |> List.filter isTheType

    match (candidates, isOptional) with
    | ( head :: [], _ ) -> head
    | ( [], true ) -> null
    | ( _ :: _, _ ) -> raise (TypeLoadException (sprintf "Too many types found for %O on %O (%A)" defaultName containerName candidates))
    | ( [], false ) -> raise (TypeLoadException (sprintf "Could not found type %O on %O" defaultName containerName))


let findModuleProperty (moduleType: Type) containerName attrType defaultName  (isOptional: bool) =
    let isTheProperty (p: PropertyInfo) =
        let attr = p.GetCustomAttributes(attrType, false)
        attr.Length > 0 || p.Name = defaultName

    let properties = moduleType.GetProperties(BindingFlags.Static ||| BindingFlags.Public)
    let candidates = properties
                    |> Array.toList
                    |> List.filter isTheProperty

    match (candidates, isOptional) with
    | ( head :: [], _ ) -> head
    | ( [], true ) -> null
    | ( _ :: _, _ ) -> raise (TypeLoadException (sprintf "Too many properties found for %O on %O" defaultName containerName))
    | ( [], false ) -> raise (TypeLoadException (sprintf "Could not found property %O on %O" defaultName containerName))
    
    
let findModuleMethod (moduleType: Type) containerName attrType defaultName (isOptional: bool) = 
    let isAMethod (m: MethodInfo) =
        let attr = m.GetCustomAttributes(attrType, false)
        attr.Length > 0 || m.Name = defaultName

    let isValidMethod (m: MethodInfo) =
        let parameters = m.GetParameters() 
                        |> Array.toList 
                        |> List.map (fun p -> p.ParameterType)
        let isValid =
            //( areSameType m.ReturnType returnType ) && 
            parameters.Length >= 1// &&
//                parameters.Length <= paramTypes.Length &&
//                paramTypes 
//                |> List.take parameters.Length 
//                |> List.zip parameters 
//                |> List.forall (fun (a, b) -> areSameType a b)
        isValid

    let methods = moduleType.GetMethods(BindingFlags.Static ||| BindingFlags.Public)
    let candidates = methods 
                    |> Array.toList
                    |> List.filter isAMethod

    match (candidates, isOptional) with
    | ( head :: [], _ ) -> 
        if isValidMethod head 
        then head
        else raise (TypeLoadException (sprintf "Found method %O on %O had invalid parameters" head containerName))
    | ( [], true ) -> null
    | ( _ :: _, _ ) -> raise (TypeLoadException (sprintf "Too many methods found for %O on %O" defaultName containerName))
    | ( [], false ) -> raise (TypeLoadException (sprintf "Could not found method %O on %O" defaultName containerName))


let getListType (elementType: Type) =
    typeOfFSharpList.MakeGenericType elementType

let getOptionType (elementType: Type) =
    typeOfFSharpOption.MakeGenericType elementType

let getSeqType (elementType: Type) =
    typeOfSeq.MakeGenericType elementType

let getMethodEnumerableCast (elementType: Type) =
    methodEnumerableCast.MakeGenericMethod elementType