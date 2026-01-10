import {useState} from 'react'
import './App.css'

function App() {
    const [prompt, setPrompt] = useState('');
    const [response, setResponse] = useState('');

    async function sendPrompt() {
        console.log(`Prompt: ${prompt}`);
        const responseFetched = await fetch("api/askAi?input=" + prompt);
        const json = await responseFetched.json();
        console.log(`Response: ${json}`);
        setResponse(json);
    }

    return (
        <>
            <div>
                <svg height="220" width="500" xmlns="http://www.w3.org/2000/svg">
                    <polygon id="logo" points="250,90 300,170 200,170" />
                </svg>
            </div>
            <h1>AI Agent</h1>
            <div className="main-container">
                <div className="card">
                    <label>Prompt</label>
                    <textarea value={prompt} onChange={(e) => setPrompt(e.target.value)}/>
                </div>
                <div className="card">
                    <label>AI response</label>
                    <textarea value={response} readOnly/>
                </div>
            </div>
            <button onClick={sendPrompt}>Send</button>
        </>
    )
}

export default App
