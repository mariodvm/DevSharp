[<DevSharp.Annotations.AggregateModule>]
module Samples.Domains.TodoList

open System
open DevSharp.Validations
open DevSharp.Validations.ValidationUtils
open DevSharp.Annotations

type TodoListTitle          = TodoListTitle of string
type TaskId                 = TaskId of int
type TaskText               = TaskText of string

type Event = 
| WasCreated                of TodoListTitle
| TitleWasUpdated           of TodoListTitle
| TaskWasAdded              of TaskId * TaskText
| TaskWasUpdated            of TaskId * TaskText
| TaskWasRemoved            of TaskId
| TaskWasChecked            of TaskId
| TaskWasUnchecked          of TaskId
    
type Command =
| Create                    of TodoListTitle
| UpdateTitle               of TodoListTitle
| AddTask                   of TaskText
| UpdateTask                of TaskId * TaskText
| RemoveTask                of TaskId
| CheckTask                 of TaskId
| UncheckTask               of TaskId
| RemoveAllTasks
| RemoveAllCheckedTasks
| CheckAllTasks
| UncheckAllTasks

type State =
    { 
        title:      TodoListTitle
        nextTaskId: TaskId
        tasks:      TodoTask list
    }    
and  TodoTask =
    { 
        id:        TaskId
        text:      TaskText
        isChecked: bool
    }

[<AggregateInit>]
let init: State option = 
    None

[<AggregateAct>]
let act command state =
    match (state, command) with
        | (None, Create title) -> 
            [ WasCreated title ]

        | (Some state, UpdateTitle newTitle) -> 
            if state.title <> newTitle 
            then [ TitleWasUpdated newTitle ] 
            else []

        | (Some state, AddTask text) ->
            [ TaskWasAdded (state.nextTaskId, text) ]

        | (Some state, UpdateTask (id, text)) ->
            state.tasks 
            |> List.filter (fun t -> t.id = id && t.text <> text) 
            |> List.map (fun t -> TaskWasUpdated (t.id, text))

        | (Some state, RemoveTask id) ->
            state.tasks 
            |> List.filter (fun t -> t.id = id) 
            |> List.map (fun t -> TaskWasRemoved t.id)

        | (Some state, CheckTask id) ->
            state.tasks 
            |> List.filter (fun t -> t.id = id && not t.isChecked) 
            |> List.map (fun t -> TaskWasChecked t.id)

        | (Some state, UncheckTask id) ->
            state.tasks 
            |> List.filter (fun t -> t.id = id && t.isChecked) 
            |> List.map (fun t -> TaskWasUnchecked t.id)

        | (Some state, RemoveAllTasks) ->
            state.tasks 
            |> List.map (fun t -> TaskWasRemoved t.id)

        | (Some state, RemoveAllCheckedTasks) ->
            state.tasks 
            |> List.filter (fun t -> t.isChecked) 
            |> List.map (fun t -> TaskWasRemoved t.id)

        | (Some state, CheckAllTasks) ->
            state.tasks 
            |> List.filter (fun t -> not t.isChecked) 
            |> List.map (fun t -> TaskWasChecked t.id)

        | (Some state, UncheckAllTasks) ->
            state.tasks 
            |> List.filter (fun t -> t.isChecked) 
            |> List.map (fun t -> TaskWasUnchecked t.id)

let getNextTaskId taskId =
    match taskId with
    | TaskId id -> TaskId (id + 1)

[<AggregateApply>]
let apply event state =
    match (state, event) with
    | (None, WasCreated title) ->
        Some { 
            title = title
            nextTaskId = TaskId 1
            tasks = []
        }

    | (Some state, TitleWasUpdated newTitle) -> 
        Some { 
        state with 
            title = newTitle
        } 

    | (Some state, TaskWasAdded (id, text)) ->
        Some { 
        state with 
            nextTaskId = getNextTaskId state.nextTaskId
            tasks = state.tasks @ 
                    [ { id = id; text = text; isChecked = false } ] 
        }

    | (Some state, TaskWasUpdated (id, text)) ->
        let updatedTask task = 
            if task.id = id 
            then { task with text = text } 
            else task
        Some { 
        state with 
            tasks = state.tasks |> List.map updatedTask 
        }

    | (Some state, TaskWasRemoved id) ->
        let differentTask task = task.id <> id
        Some { 
        state with 
            tasks = state.tasks |> List.filter differentTask
        }

    | (Some state, TaskWasChecked id) ->
        let checkedTask task = 
            if task.id = id && not task.isChecked 
            then { task with isChecked = true } 
            else task
        Some { 
        state with 
            tasks = state.tasks 
                    |> List.map checkedTask 
        }

    | (Some state, TaskWasUnchecked id) ->
        let uncheckedTask task = 
            if task.id = id && task.isChecked 
            then { task with isChecked = false } 
            else task
        Some { 
        state with 
            tasks = state.tasks 
                    |> List.map uncheckedTask 
        }

[<AggregateValidate>]
let validate command =
    let validateId taskId =
        seq {
            match taskId with
            | TaskId id ->
                if id <= 0 
                then yield memberFailure "id" "Id must be positive"
        }

    let validateTitle title =
        seq {
            match title with
            | TodoListTitle t ->
                if String.IsNullOrEmpty t
                then yield memberFailure "title" "Title cannot be empty"
                else if t.Length < 4 || t.Length > 100 
                    then yield memberFailure "title" "Title length must be between 4 and 100"
        }

    let validateTaskText text =
        seq {
            match text with
            | TaskText t ->
                if String.IsNullOrEmpty t 
                then yield memberFailure "text" "Task text cannot be empty"
                else if t.Length < 4 || t.Length > 100 
                    then yield memberFailure "text" "Task text length must be between 4 and 100"
        }

    in
    match command with
        | Create title -> validateTitle title
        | UpdateTitle title -> validateTitle title
        | AddTask text -> validateTaskText text
        | UpdateTask (id, text) ->
            seq { 
                yield! validateId id
                yield! validateTaskText text
            }
        | RemoveTask id -> validateId id
        | CheckTask id -> validateId id
        | UncheckTask id -> validateId id
        | RemoveAllTasks -> Seq.empty
        | RemoveAllCheckedTasks -> Seq.empty
        | CheckAllTasks -> Seq.empty
        | UncheckAllTasks -> Seq.empty
