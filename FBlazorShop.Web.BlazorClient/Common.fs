module Common

open Elmish
open System

type Message =
    | AuthenticationRequested
    | Error of exn

let authenticationRequested  = Cmd.ofMsg (AuthenticationRequested)

type Authentication = {
    User : string;
    Token : string;
    TimeStamp : DateTime;
}

type State = { 
    Authentication : Authentication option;
}