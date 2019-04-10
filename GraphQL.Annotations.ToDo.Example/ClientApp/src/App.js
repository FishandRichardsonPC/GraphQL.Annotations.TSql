import "react-dates/initialize"
import React, { Component } from "react"
import "./App.css"
import "icono/dist/icono.min.css"
import {
	ApolloClient,
	ApolloProvider,
	createNetworkInterface
} from "react-apollo"
import ToDoList from "./components/ToDoList"
import "react-dates/lib/css/_datepicker.css"

const styles = {
	fontFamily: "sans-serif",
	textAlign: "center"
}
class App extends Component {
	createClient() {
		// Initialize Apollo Client with URL to our server
		return new ApolloClient({
			networkInterface: createNetworkInterface({
				uri: "/graphql"
				// dataIdFromObject: o => o.id
			})
		})
	}
	render() {
		return (
			<ApolloProvider client={this.createClient()}>
				<div className="app-container">
					<div className="title-container">
                        <a href="/playground">GraphQL Playground</a>
						<h1>ToDo list</h1>
					</div>
					<ToDoList />
				</div>
			</ApolloProvider>
		)
	}
}

export default App
