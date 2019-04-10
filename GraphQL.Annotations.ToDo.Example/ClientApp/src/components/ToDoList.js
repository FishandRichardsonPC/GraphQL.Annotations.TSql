//@flow
import React from "react"
import ToDoItem from "./ToDoItem.js"

import type { OperationComponent } from "react-apollo"
import type { ToDo, priority } from "../flowTypes"
import {
	gql,
	graphql,
	compose
} from "react-apollo"
import "./ToDoList.css"
type SortType = "name" | "priority" | "due-date"
type State = {
	text: string,
	sortType: SortType
}
type Props = {
	addToDo: ({ text: string }) => void,
	deleteToDo: string => Promise<string>,
	editToDo: (toDo: ToDo) => Promise<ToDo>,
	data: {
		loading: boolean,
		error: { message: string },
		toDos: Array<ToDo>
	}
}
const sortBy = (sortType: SortType) => (a: ToDo, b: ToDo) => {
	let comparableA;
	let comparableB;
	const priorityMap = {
		HIGH: 3,
		MEDIUM: 2,
		LOW: 1
	};

	const nameComparableA = a.text.toUpperCase();
	const nameComparableB = b.text.toUpperCase();
	if (sortType === "priority") {
		comparableA = priorityMap[a.priority];
		comparableB = priorityMap[b.priority]
	} else if (sortType === "due-date") {
		comparableB = b.dueDate ? new Date(b.dueDate).getTime() : -1;
		comparableA = a.dueDate ? new Date(a.dueDate).getTime() : -1
	}
	if (comparableA !== undefined && comparableB !== undefined) {
		if (comparableA < comparableB) {
			return 1
		}
		//$FlowFixMe:
		if (comparableA > comparableB) {
			return -1
		}
	}
	//equal, sort by name
	if (nameComparableA < nameComparableB) {
		return -1
	} else if (nameComparableA > nameComparableB) {
		return 1
	} else {
		return 0
	}
};
class ToDoList extends React.Component<Props, State> {
	constructor(props: Props) {
		super(props);
		this.state = {
			text: "",
			sortType: "name"
		}
	}
	onEnterClick(event: SyntheticInputEvent<*>) {
		if (event.which == 13 || event.keyCode == 13) {
			this.addToDo();
			this.setState({
				text: ""
			})
		}
	}
	addToDo() {
		const { text } = this.state;
		const toDo = {
			text
		};
		return this.props.addToDo(toDo)
	}
	deleteToDo(id) {
		this.props.deleteToDo(id).then(id => {
			return id
		})
	}
	editToDo(toDo: ToDo) {
		return this.props.editToDo(toDo).then(toDo => {
			return toDo
		})
	}
	setSortType = (sortType: SortType) => () => {
		this.setState({
			sortType: sortType
		})
	};
	render() {
		const { sortType, text } = this.state;
		const { data: { loading, error, toDos } } = this.props;
		if (loading) {
			return <p>Loading ...</p>
		}
		if (error) {
			return <p>{error.message}</p>
		}
		//sort mutate the array
		const sortedToDos = [...toDos].sort(sortBy(sortType));
		return (
			<div className="container">
				<div className="header">
					<input
						type="text"
						value={text}
						className="new-toDo"
						placeholder="To do or not to do, that is the question"
						onKeyPress={this.onEnterClick.bind(this)}
						onChange={(event: SyntheticInputEvent<*>) => {
							const text = event.target.value;
							this.setState({ text })
						}}
					/>
				</div>
				<div className="main">
					<div className="sort-type__container">
						<span className="description">Sort By</span>
						<button
							className={`${sortType === "name" ? "active" : ""}`}
							onClick={this.setSortType.bind(this)("name")}
						>
							Name
						</button>
						<button
							className={`${sortType === "due-date" ? "active" : ""}`}
							onClick={this.setSortType.bind(this)("due-date")}
						>
							Due Date
						</button>
						<button
							className={`${sortType === "priority" ? "active" : ""}`}
							onClick={this.setSortType.bind(this)("priority")}
						>
							Priority
						</button>
					</div>
					<ul className="toDo-list">
						{sortedToDos.map(toDo => (
							<ToDoItem
								onDeleteClick={this.deleteToDo.bind(this)}
								handleEdit={this.editToDo.bind(this)}
								key={toDo.id}
								toDo={toDo}
							/>
						))}
					</ul>
				</div>
				<div />
			</div>
		)
	}
}
const getToDos = gql`
	{
		toDos {
			id
			text
			priority
			dueDate
		}
	}
`;

const addToDo = gql`
	mutation addToDo($toDo: ToDoMutable!) {
		toDo(toDo: $toDo) {
            id
        }
	}
`;
const editToDo = gql`
	mutation editToDo($toDo: ToDoMutable!) {
        toDo(toDo: $toDo) {
			id
			text
			priority
			dueDate
			completed
		}
	}
`;

const deleteToDo = gql`
	mutation deleteToDo($id: String!) {
		toDo_delete(id: $id)
	}
`;
type DeleteResult = {}
type AddResult = {
	addToDo: string
}
type EditResult = {
	editToDo: ToDo
}
type GetResult = {
	toDos: Array<ToDo>
}
const withEdit: OperationComponent<EditResult> = graphql(editToDo, {
	props: ({ mutate }) => ({
		editToDo: (toDo: ToDo) =>
			mutate({
				variables: { toDo },
				//https://github.com/apollographql/apollo-client/issues/899
				//$FlowFixMe
				refetchQueries: [{ query: getToDos }]
			})
	})
});
const withGet: OperationComponent<GetResult> = graphql(getToDos);
const withDelete: OperationComponent<DeleteResult> = graphql(deleteToDo, {
	props: ({ mutate }) => ({
		deleteToDo: (toDoId: string) =>
			mutate({
				variables: { id: toDoId },
				//https://github.com/apollographql/apollo-client/issues/899
				//$FlowFixMe
				refetchQueries: [{ query: getToDos }]
			})
	})
});
const withAdd: OperationComponent<AddResult> = graphql(addToDo, {
	props: ({ mutate }) => ({
		addToDo: (toDo: ToDo) =>
			mutate({
				variables: { toDo: {
				    ...toDo,
                    id: null
                } },
				//$FlowFixMe
				refetchQueries: [{ query: getToDos }]
			})
	})
});

const withToDos = compose(withGet, withAdd, withEdit, withDelete);
export default withToDos(ToDoList)
