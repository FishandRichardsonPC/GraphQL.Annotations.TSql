//@flow
import React from "react"
import "./ToDoItem.css"
//Share flow types with server
import type { ToDo } from "../flowTypes"
import { SingleDatePicker } from "react-dates"
import moment from "moment"
type Props = {
	toDo: ToDo,
	onDeleteClick: string => void,
	handleEdit: (toDo: ToDo) => Promise<ToDo>
}
type State = {
	isEditing: boolean,
	temporaryText: string,
	focused: boolean
}
const priorityMap = {
	UP: {
		HIGH: "HIGH",
		MEDIUM: "HIGH",
		LOW: "MEDIUM"
	},
	DOWN: {
		HIGH: "MEDIUM",
		MEDIUM: "LOW",
		LOW: "LOW"
	}
};
export default class ToDoItem extends React.Component<Props, State> {
	constructor(props: Props) {
		super(props)
		this.state = {
			isEditing: false,
			temporaryText: props.toDo.text,
			focused: false
		}
	}
	onInputClick() {
		this.setState({ isEditing: true })
	}
	onKeyPress(event: SyntheticInputEvent<*>) {
		const { isEditing, temporaryText } = this.state
		const { toDo: { id } } = this.props
		if (event.which == 13) {
			if (this.state.isEditing && temporaryText !== this.props.text) {
				this.props.handleEdit({ id, text: temporaryText })
				this.setState({ isEditing: false })
			}
		}
	}
	onKeyDown(event: SyntheticInputEvent<*>) {
		if (event.which == 27) {
			this.setState({ isEditing: false })
		}
	}
	onInputChange(event: SyntheticInputEvent<HTMLInputElement>) {
		const text = event.target.value
		this.setState({ temporaryText: text })
	}
	movePriority(upOrDown: "UP" | "DOWN") {
		const { toDo: { id, priority }, handleEdit } = this.props

		let targetPriority = priorityMap[upOrDown][priority]
		handleEdit({ id, priority: targetPriority })
	}
	handleChecked(event: SyntheticInputEvent<HTMLInputElement>) {
		const { toDo: { id }, handleEdit } = this.props
		const completed = event.target.checked
		handleEdit({ id, completed })
	}
	handleDueDateChange(date) {
		const { toDo: { id }, handleEdit } = this.props
		handleEdit({ id, dueDate: moment(date).format() })
	}
	render() {
		const { isEditing, temporaryText } = this.state
		const { toDo, onDeleteClick } = this.props

		return (
			<li className="item-container">
				<div className="view">
					<input
						name="toDo"
						className="toggle"
						type="checkbox"
						value={toDo.completed}
						onChange={this.handleChecked.bind(this)}
					/>
					{isEditing ? (
						<input
							onKeyPress={this.onKeyPress.bind(this)}
							onKeyDown={this.onKeyDown.bind(this)}
							type="text"
							className="edit"
							value={temporaryText}
							onChange={this.onInputChange.bind(this)}
						/>
					) : (
						<label
							className="label"
							htmlFor="toDo"
							onClick={this.onInputClick.bind(this)}
						>
							{temporaryText}
						</label>
					)}
					<div className="date-picker__container">
						{/* <span>due</span> */}
						<SingleDatePicker
							placeholder={"Due Date"}
							date={toDo.dueDate ? moment(toDo.dueDate) : null} // momentPropTypes.momentObj or null
							onDateChange={this.handleDueDateChange.bind(this)} // PropTypes.func.isRequired
							focused={this.state.focused} // PropTypes.bool
							onFocusChange={({ focused }) => this.setState({ focused })} // PropTypes.func.isRequired
						/>
					</div>
					<div className="priorityContainer">
						<button
							className="up icono-caretUp"
							onClick={() => this.movePriority.bind(this)("UP")}
						/>
						<span className="priority">{toDo.priority}</span>
						<button
							className="down icono-caretDown"
							onClick={() => this.movePriority.bind(this)("DOWN")}
						/>
					</div>
					<button className="delete" onClick={() => onDeleteClick(toDo.id)}>
						X
					</button>
				</div>
			</li>
		)
	}
}
