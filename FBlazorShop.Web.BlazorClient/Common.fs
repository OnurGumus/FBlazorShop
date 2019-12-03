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
type AuthState = NotTried | Failed | Success of Authentication
type State = { 
    Authentication : AuthState;
}