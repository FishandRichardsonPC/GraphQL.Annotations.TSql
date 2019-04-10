//@flow
export type priority = "HIGH" | "MEDIUM" | "LOW"

export type ToDo = {
	id: string,
	text: string,
	dueDate?: string,
	priority: priority,
	completed: boolean
}
